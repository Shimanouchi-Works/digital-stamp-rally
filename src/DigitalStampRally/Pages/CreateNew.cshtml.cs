using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DigitalStampRally.Services;
using DigitalStampRally.Models;

namespace DigitalStampRally.Pages;

public class CreateNewModel : PageModel
{
    private readonly IProjectDraftStore _draftStore;
    private readonly IProjectStore _projectStore;

    public CreateNewModel(
                IProjectDraftStore draftStore,
                IProjectStore projectStore)
    {
        _draftStore = draftStore;
        _projectStore = projectStore;

        // QuestPDF ライセンス（Community）
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [BindProperty]
    public CreateNewInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet(string? load)
    {
        // デフォルト値
        if (Input.ValidFrom == default) Input.ValidFrom = DateTime.Now.AddMinutes(5);
        if (Input.ValidTo == default) Input.ValidTo = DateTime.Now.AddHours(6);

        // loadトークンがあればドラフト復元
        if (!string.IsNullOrWhiteSpace(load))
        {
            if (_draftStore.TryGet(load, out var json))
            {
                try
                {
                    var draft = JsonSerializer.Deserialize<ProjectDraftDto>(json, JsonOptions());
                    if (draft != null)
                    {
                        Input.EventTitle = draft.EventTitle ?? "";
                        Input.ValidFrom = draft.ValidFrom;
                        Input.ValidTo = draft.ValidTo;

                        Input.Spots = draft.Spots?.Select(s => new SpotInputModel
                        {
                            SpotName = s.SpotName ?? "",
                            IsRequired = s.IsRequired
                        }).ToList() ?? new List<SpotInputModel> { new() };

                        // 使い切りにしたい場合は消してOK（必要ならこの行を外す）
                        _draftStore.Remove(load);
                    }
                }
                catch
                {
                    ErrorMessage = "読み出したJSONの解析に失敗しました（形式が不正の可能性があります）。";
                }
            }
            else
            {
                ErrorMessage = "読み出しトークンが無効、または期限切れです。Indexから再度読み出してください。";
            }
        }

        // Spotsが空なら1件
        if (Input.Spots.Count == 0) Input.Spots.Add(new SpotInputModel());
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        // 入力の基本チェック
        if (!ModelState.IsValid)
            return Page();

        // Spots追加バリデーション（空・重複）
        var cleaned = Input.Spots
            .Select(s => (s.SpotName ?? "").Trim())
            .ToList();

        if (cleaned.Count < 1)
        {
            ErrorMessage = "掲示場所は最低1件必要です。";
            return Page();
        }

        if (cleaned.Any(string.IsNullOrWhiteSpace))
        {
            ErrorMessage = "掲示場所名が空の行があります。";
            return Page();
        }

        if (cleaned.Distinct(StringComparer.OrdinalIgnoreCase).Count() != cleaned.Count)
        {
            ErrorMessage = "掲示場所名が重複しています。";
            return Page();
        }

        if (Input.ValidFrom >= Input.ValidTo)
        {
            ErrorMessage = "有効期間（開始）は（終了）より前にしてください。";
            return Page();
        }

        // 画像（任意）
        EventImageDto? eventImage = null;
        if (Input.EventImageFile != null && Input.EventImageFile.Length > 0)
        {
            const long maxBytes = 2 * 1024 * 1024; // 2MB
            if (Input.EventImageFile.Length > maxBytes)
            {
                ErrorMessage = "イベント画像が大きすぎます（最大 2MB）。";
                return Page();
            }

            await using var ms = new MemoryStream();
            await Input.EventImageFile.CopyToAsync(ms);
            eventImage = new EventImageDto
            {
                FileName = Input.EventImageFile.FileName,
                ContentType = Input.EventImageFile.ContentType ?? "application/octet-stream",
                Base64 = Convert.ToBase64String(ms.ToArray())
            };
        }

        // プロジェクト生成（MVP: ランダムトークン方式）
        var project = BuildProject(eventImage);
        await _projectStore.SaveAsync(project);

        // ZIP出力（PDF×(スポット数+2) + project.json）
        var zipBytes = BuildZipPackage(project);

        var safeTitle = SanitizeFileName(Input.EventTitle);
        var fileName = $"StampRally_{safeTitle}_{DateTime.Now:yyyyMMdd_HHmm}.zip";
        return File(zipBytes, "application/zip", fileName);
    }

    // --------------------
    // Project build
    // --------------------
    private ProjectDto BuildProject(EventImageDto? image)
    {
        var eventId = Guid.NewGuid().ToString("N");
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var spots = Input.Spots.Select(s => new SpotDto
        {
            SpotId = Guid.NewGuid().ToString("N"),
            SpotName = s.SpotName.Trim(),
            IsRequired = s.IsRequired,
            SpotToken = RandomToken(16)
        }).ToList();

        var totalizeToken = RandomToken(24);
        var totalizePassword = RandomPassword(10);

        // ゴール用
        var goalToken = RandomToken(24);

        return new ProjectDto
        {
            Version = 1,
            EventId = eventId,
            EventTitle = Input.EventTitle.Trim(),
            ValidFrom = Input.ValidFrom,
            ValidTo = Input.ValidTo,
            EventImage = image,

            Spots = spots,

            GoalToken = goalToken,
            TotalizeToken = totalizeToken,
            TotalizePassword = totalizePassword,

            // URLテンプレ（PDF内のQR生成に使う）
            Urls = new UrlsDto
            {
                ReadStampBase = $"{baseUrl}/ReadStamp",
                SetGoalBase = $"{baseUrl}/SetGoal",
                TotalizeBase = $"{baseUrl}/Totalize"
            }
        };
    }

    // --------------------
    // ZIP build
    // --------------------
    private byte[] BuildZipPackage(ProjectDto project)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // project.json
            var json = JsonSerializer.Serialize(project, JsonOptions());
            AddText(zip, "project.json", json);

            // スポット掲示用PDF（各スポット）
            foreach (var spot in project.Spots)
            {
                var url = $"{project.Urls.ReadStampBase}?e={project.EventId}&s={spot.SpotId}&t={spot.SpotToken}";
                var pdf = BuildPosterPdf(project, spot.SpotName, spot.IsRequired, url);
                AddBytes(zip, $"posters/spot_{SanitizeFileName(spot.SpotName)}.pdf", pdf);
            }

            // ゴール用PDF
            {
                var url = $"{project.Urls.SetGoalBase}?e={project.EventId}&t={project.GoalToken}";
                var pdf = BuildSimpleQrPdf(
                    title: "利用者ゴールQRコード",
                    subtitle: "主催者が管理するQRです。来場者がゴール時に読み取ります。",
                    eventTitle: project.EventTitle,
                    validFrom: project.ValidFrom,
                    validTo: project.ValidTo,
                    url: url,
                    eventImage: GetEventImageBytes(project.EventImage)
                );
                AddBytes(zip, "goal/goal_qr.pdf", pdf);
            }

            // 集計画面アクセス用PDF
            {
                var url = $"{project.Urls.TotalizeBase}?e={project.EventId}&t={project.TotalizeToken}";
                var pdf = BuildTotalizePdf(project, url);
                AddBytes(zip, "totalize/totalize_access.pdf", pdf);
            }
        }

        return ms.ToArray();
    }

    private static void AddText(ZipArchive zip, string path, string text)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var s = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(text);
        s.Write(bytes, 0, bytes.Length);
    }

    private static void AddBytes(ZipArchive zip, string path, byte[] bytes)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(bytes, 0, bytes.Length);
    }

    // --------------------
    // PDF builders
    // --------------------
    private byte[] BuildPosterPdf(ProjectDto project, string spotName, bool isRequired, string url)
    {
        var qrPng = BuildQrPng(url);
        var eventImage = GetEventImageBytes(project.EventImage);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Content().Column(col =>
                {
                    col.Item().Text(project.EventTitle).FontSize(22).SemiBold();

                    if (eventImage != null)
                    {
                        col.Item().PaddingTop(8).Image(eventImage).FitWidth();
                    }

                    col.Item().PaddingTop(14).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text($"掲示場所：{spotName}").FontSize(16).SemiBold();
                            left.Item().PaddingTop(6).Text(isRequired ? "この場所は【必須スタンプ】です" : "この場所は必須ではありません");
                            left.Item().PaddingTop(10).Text($"有効期限：{project.ValidFrom:yyyy/MM/dd HH:mm} ～ {project.ValidTo:yyyy/MM/dd HH:mm}");
                        });

                        row.ConstantItem(180).AlignMiddle().AlignCenter()
                            .Border(1).Padding(8)
                            .Column(q =>
                            {
                                q.Item().Image(qrPng).FitArea();
                                q.Item().PaddingTop(6).Text("読み取りはこちら").FontSize(10).AlignCenter();
                            });
                    });

                    col.Item().PaddingTop(16).Text("読み取り方法：スマホのカメラでQRを読み取り、表示された画面の指示に従ってください。");

                    col.Item().PaddingTop(8).Text("注意：")
                        .SemiBold();

                    col.Item().Text("・通信が必要です");
                    col.Item().Text("・同じ端末（同じブラウザ）で集める必要があります");
                });
            });
        }).GeneratePdf();
    }

    private byte[] BuildTotalizePdf(ProjectDto project, string url)
    {
        var qrPng = BuildQrPng(url);
        var eventImage = GetEventImageBytes(project.EventImage);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Content().Column(col =>
                {
                    col.Item().Text("集計画面アクセス用QRコード").FontSize(22).SemiBold();
                    col.Item().Text(project.EventTitle).FontSize(14).FontColor(Colors.Grey.Darken2);

                    if (eventImage != null)
                        col.Item().PaddingTop(8).Image(eventImage).FitWidth();

                    col.Item().PaddingTop(14).Row(row =>
                    {
                        row.ConstantItem(220).Border(1).Padding(10).AlignMiddle().AlignCenter()
                            .Column(c =>
                            {
                                c.Item().Image(qrPng).FitArea();
                                c.Item().PaddingTop(6).Text("集計画面を開く").FontSize(10).AlignCenter();
                            });

                        row.RelativeItem().PaddingLeft(16).Column(r =>
                        {
                            r.Item().Text("アクセス方法").SemiBold();
                            r.Item().Text("1) QRを読み取る（またはURLを入力）");
                            r.Item().Text("2) パスワードを入力");

                            r.Item().PaddingTop(10).Text("パスワード").SemiBold();
                            r.Item().Text(project.TotalizePassword).FontSize(18).SemiBold();

                            r.Item().PaddingTop(10).Text($"有効期限：{project.ValidFrom:yyyy/MM/dd HH:mm} ～ {project.ValidTo:yyyy/MM/dd HH:mm}");
                            r.Item().PaddingTop(8).Text("※ パスワードは後から変更可能（実装予定）").FontColor(Colors.Grey.Darken2);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private byte[] BuildSimpleQrPdf(string title, string subtitle, string eventTitle, DateTime validFrom, DateTime validTo, string url, byte[]? eventImage)
    {
        var qrPng = BuildQrPng(url);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Content().Column(col =>
                {
                    col.Item().Text(title).FontSize(22).SemiBold();
                    col.Item().Text(subtitle).FontColor(Colors.Grey.Darken2);

                    col.Item().PaddingTop(6).Text(eventTitle).FontSize(14).SemiBold();

                    if (eventImage != null)
                        col.Item().PaddingTop(8).Image(eventImage).FitWidth();

                    col.Item().PaddingTop(16).Border(1).Padding(10).AlignCenter()
                        .Column(c =>
                        {
                            c.Item().Image(qrPng).FitArea();
                            c.Item().PaddingTop(6).Text("QRを読み取る").FontSize(10);
                        });

                    col.Item().PaddingTop(12).Text($"有効期限：{validFrom:yyyy/MM/dd HH:mm} ～ {validTo:yyyy/MM/dd HH:mm}");
                });
            });
        }).GeneratePdf();
    }

    // --------------------
    // Helpers
    // --------------------
    private static byte[] BuildQrPng(string text)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(pixelsPerModule: 8);
    }

    private static byte[]? GetEventImageBytes(EventImageDto? img)
    {
        if (img == null || string.IsNullOrWhiteSpace(img.Base64))
            return null;

        try { return Convert.FromBase64String(img.Base64); }
        catch { return null; }
    }

    private static string RandomToken(int bytes)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToHexString(Guid.NewGuid().ToByteArray())
            + Convert.ToHexString(buffer);
    }

    private static string RandomPassword(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(chars[Random.Shared.Next(chars.Length)]);
        return sb.ToString();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "event";
        return cleaned.Length > 40 ? cleaned[..40] : cleaned;
    }

    private static JsonSerializerOptions JsonOptions()
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

    // --------------------
    // Models (Page input)
    // --------------------
    public class CreateNewInputModel
    {
        [Required, MaxLength(80)]
        public string EventTitle { get; set; } = "";

        [Required]
        public DateTime ValidFrom { get; set; }

        [Required]
        public DateTime ValidTo { get; set; }

        public IFormFile? EventImageFile { get; set; }

        public List<SpotInputModel> Spots { get; set; } = new() { new SpotInputModel() };
    }

    public class SpotInputModel
    {
        public string SpotName { get; set; } = "";
        public bool IsRequired { get; set; }
    }

}
