using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private readonly DbEventService _eventService;
    private readonly IConfiguration _config;
    private readonly ILogger<CreateNewModel> _logger;

    public CreateNewModel(
        IProjectDraftStore draftStore,
        DbEventService eventService,
        IConfiguration config,
        ILogger<CreateNewModel> logger)
    {
        _draftStore = draftStore;
        _eventService = eventService;
        _config = config;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [BindProperty]
    public CreateNewInputModel Input { get; set; } = new();

    public int MaxSpotsForUi { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet(string? load)
    {
        _logger.LogInformation("Accessed CreateNew page with load token: {LoadToken}", load);

        ViewData["NoIndex"] = true; // ロボット防止
        try
        {
            // デフォルト値
            var now = DateTime.Now;
            if (Input.ValidFrom == default)
            {
                Input.ValidFrom = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            }
            if (Input.ValidTo == default)
            {
                Input.ValidTo = Input.ValidFrom.AddDays(1);
            }

            string toDateTime(DateTime dt) => dt.ToString("yyyy-MM-ddTHH:mm");
            var validMonths = _config["AppConfig:EventValidMonth"] ?? "3";
            var validFromMin = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
            var validFromMax = validFromMin.AddMonths(int.Parse(validMonths)).AddMinutes(-1);
            Input.ValidFromMin = toDateTime(validFromMin);
            Input.ValidFromMax = toDateTime(validFromMax);
            Input.ValidToMin = toDateTime(validFromMin);
            Input.ValidToMax = toDateTime(validFromMax);

            var maxSpots = _config["AppConfig:MaxSpots"] ?? "30";
            MaxSpotsForUi = int.Parse(maxSpots);

            // loadトークンがあればドラフト復元（JSON＋画像）
            if (!string.IsNullOrWhiteSpace(load))
            {
                if (_draftStore.TryGet(load, out ProjectDraftPayload payload))
                {
                    try
                    {
                        var draft = JsonSerializer.Deserialize<StampRallyProjectExport>(payload.Json, JsonOptions());
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

                            // ★ZIPから復元した画像があればInputに保持
                            if (payload.EventImage != null &&
                                payload.EventImage.Bytes != null &&
                                payload.EventImage.Bytes.Length > 0)
                            {
                                Input.LoadedEventImage = new EventImageDto
                                {
                                    FileName = payload.EventImage.FileName ?? "event",
                                    ContentType = payload.EventImage.ContentType ?? "application/octet-stream",
                                    Base64 = Convert.ToBase64String(payload.EventImage.Bytes)
                                };
                            }

                            Input.LoadToken = load;
                            // 使い切りにしたい場合は消してOK
                            //_draftStore.Remove(load);
                        }
                    }
                    catch
                    {
                        ErrorMessage = "読み出しエラー(001)。";//"読み出したproject.jsonの解析に失敗しました（形式が不正の可能性があります）。";
                    }
                }
                else
                {
                    ErrorMessage = "読み出しエラー(002)。";//"読み出しトークンが無効、または期限切れです。Indexから再度読み出してください。";
                }
            }

            // Spotsが空なら1件
            if (Input.Spots.Count == 0) Input.Spots.Add(new SpotInputModel());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorMessage = "不明なエラー";
        }
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        try
        {
            // ★POSTでは LoadedEventImage が保持されないので、LoadToken から再復元する
            if (Input.LoadedEventImage == null &&
                !string.IsNullOrWhiteSpace(Input.LoadToken) &&
                _draftStore.TryGet(Input.LoadToken, out ProjectDraftPayload payload))
            {
                if (payload.EventImage != null && payload.EventImage.Bytes.Length > 0)
                {
                    Input.LoadedEventImage = new EventImageDto
                    {
                        FileName = payload.EventImage.FileName ?? "event",
                        ContentType = payload.EventImage.ContentType ?? "application/octet-stream",
                        Base64 = Convert.ToBase64String(payload.EventImage.Bytes)
                    };
                }
            }

            if (!ModelState.IsValid)
                return Page();

            if (!Input.AgreeToTerms)
            {
                ModelState.AddModelError("", "利用規約への同意が必要です。");
                return Page();
            }

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

            // 画像（任意）※DBには保存しない（ZIP + PDF用）
            EventImageDto? eventImage = null;

            // (1) ユーザーがアップロードした画像
            if (Input.EventImageFile != null && Input.EventImageFile.Length > 0)
            {
                const long maxBytes = DigitalStampRally.Models.AppConst.MaxEventImageBytes;
                if (Input.EventImageFile.Length > maxBytes)
                {
                    ErrorMessage = $"イベント画像が大きすぎます（最大 {DigitalStampRally.Models.AppConst.MaxEventImageBytes / 1024 / 1024}MB）。";
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

            // (2) ZIPから復元された画像（アップロードが無い場合に採用）
            if (eventImage == null && Input.LoadedEventImage != null)
            {
                eventImage = Input.LoadedEventImage;
            }

            // =========================
            // ★ DB保存（events / spots / rewards）
            // =========================
            var spotInputs = Input.Spots
                .Select(s => (Name: s.SpotName.Trim(), IsRequired: s.IsRequired))
                .ToList();

            CreateEventResult created;
            try
            {
                created = await _eventService.CreateEventAsync(
                    title: Input.EventTitle.Trim(),
                    startsAt: Input.ValidFrom,
                    endsAt: Input.ValidTo,
                    spots: spotInputs
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex}");
                ErrorMessage = "エラーが発生しました。時間をおいて再度お試しください。";//"DBへの保存に失敗しました。時間をおいて再度お試しください。";
                return Page();
            }

            // project.json / PDF 生成用 ProjectDto を構築
            var project = BuildProjectFromDb(created, eventImage);

            // ZIP出力（project.json + images + PDF）
            var zipBytes = BuildZipPackage(project);

            var safeTitle = SanitizeFileName(Input.EventTitle);
            var fileName = $"StampRally_{safeTitle}_{DateTime.Now:yyyyMMdd_HHmm}.zip";

            if (!string.IsNullOrWhiteSpace(Input.LoadToken))
            {
                //_draftStore.Remove(Input.LoadToken); 画面遷移なしで再作成した場合を考慮して明示的には削除しない。時間で消える
            }

            // ローカルにも保存
            SaveLocalFile(zipBytes);

            return File(zipBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorMessage = "不明なエラー";
            return Page();
        }
    }

    // --------------------
    // Project build (from DB result)
    // --------------------
    private ProjectDto BuildProjectFromDb(CreateEventResult created, EventImageDto? image)
    {
        var baseUrl = _config["QrBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";

        var spots = created.Spots.Select(s => new SpotDto
        {
            SpotId = s.SpotId.ToString(),
            SpotName = s.Name,
            IsRequired = s.IsRequired,
            SpotToken = s.SpotToken
        }).ToList();

        return new ProjectDto
        {
            Version = 1,
            EventId = created.EventId.ToString(),

            EventTitle = Input.EventTitle.Trim(),
            ValidFrom = Input.ValidFrom,
            ValidTo = Input.ValidTo,
            EventImage = image,

            Spots = spots,

            GoalToken = created.GoalToken,
            TotalizeToken = created.TotalizeToken,
            TotalizePassword = created.TotalizePassword,

            Urls = new UrlsDto
            {
                ReadStampBase = $"{baseUrl}/ReadStamp",
                SetGoalBase = $"{baseUrl}/SetGoal",
                TotalizeBase = $"{baseUrl}/Totalize"
            }
        };
    }

    // --------------------
    // ZIP build (project.json + images + pdfs)
    // --------------------
    private byte[] BuildZipPackage(ProjectDto project)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 画像をZIPに格納し、manifestに参照情報を持たせる
            EventImageRef? imageRef = null;

            if (project.EventImage != null && !string.IsNullOrWhiteSpace(project.EventImage.Base64))
            {
                var imgBytes = GetEventImageBytes(project.EventImage);

                if (imgBytes != null && imgBytes.Length > 0)
                {
                    var ext = GuessImageExtension(project.EventImage.ContentType, project.EventImage.FileName);
                    var imagePathInZip = $"images/event{ext}";

                    //AddBytes(zip, imagePathInZip, imgBytes);

                    imageRef = new EventImageRef
                    {
                        FileName = imagePathInZip,
                        ContentType = project.EventImage.ContentType ?? "application/octet-stream",
                        SizeBytes = imgBytes.LongLength,
                        Sha256 = ComputeSha256Hex(imgBytes)
                    };
                }
            }

            // project.json（最小 + 画像参照）
            // var export = new StampRallyProjectExport
            // {
            //     App = "Qmikke",
            //     Format = "stamp-rally-project",
            //     Version = 1,

            //     EventTitle = project.EventTitle,
            //     ValidFrom = project.ValidFrom,
            //     ValidTo = project.ValidTo,

            //     EventImage = imageRef,

            //     Spots = project.Spots.Select(s => new SpotExport
            //     {
            //         SpotName = s.SpotName,
            //         IsRequired = s.IsRequired
            //     }).ToList()
            // };

            // var json = JsonSerializer.Serialize(export, JsonOptions());
            // AddText(zip, "project.json", json);
            // ★復元用パッケージ（project.qmkpj）を外側ZIPに同梱
            var qmkpjBytes = BuildProjectPackageQmkpj(project);
            AddBytes(zip, AppConst.QmkpjFileName, qmkpjBytes);

            // // スポット掲示用PDF（各スポット）  
            // foreach (var spot in project.Spots)
            // {
            //     var url = $"{project.Urls.ReadStampBase}?e={project.EventId}&s={spot.SpotId}&t={spot.SpotToken}";
            //     _logger.LogInformation("Generating poster PDF for Spot '{SpotName}' with URL: {Url}", spot.SpotName, url);

            //     var pdf = BuildPosterPdf(project, spot.SpotName, spot.IsRequired, url);
            //     AddBytes(zip, $"各掲示場所に掲示するPDF/spot_{SanitizeFileName(spot.SpotName)}.pdf", pdf);
            // }
            // 掲示場所まとめPDF（1場所=1ページ、1ファイル）
            {
                _logger.LogInformation("Generating spots posters PDF (all-in-one). SpotsCount={Count}", project.Spots.Count);

                var pdf = BuildSpotsPosterPdfAllInOne(project);
                AddBytes(zip, $"各掲示場所に掲示するPDF/掲示場所まとめ.pdf", pdf);
            }

            // ゴール用PDF
            {
                var url = $"{project.Urls.SetGoalBase}?e={project.EventId}&t={project.GoalToken}";
                _logger.LogInformation("Generating goal PDF with URL: {Url}", url);

                var pdf = BuildSimpleQrPdf(
                    title: "利用者ゴールQRコード",
                    subtitle: "主催者が管理するQRです。来場者がゴール時に読み取ります。",
                    eventTitle: project.EventTitle,
                    validFrom: project.ValidFrom,
                    validTo: project.ValidTo,
                    url: url,
                    eventImage: GetEventImageBytes(project.EventImage)
                );
                AddBytes(zip, "主催者が保有するPDF/ゴールした参加者が読むPDF.pdf", pdf);
            }

            // 集計画面アクセス用PDF
            {
                var url = $"{project.Urls.TotalizeBase}?e={project.EventId}&t={project.TotalizeToken}";
                var pdf = BuildTotalizePdf(project, url);
                AddBytes(zip, "主催者が保有するPDF/主催者が集計画面をみるためのPDF.pdf", pdf);
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
    // private byte[] BuildPosterPdf(ProjectDto project, string spotName, bool isRequired, string url)
    // {
    //     var qrPng = BuildQrPng(url);
    //     var eventImage = GetEventImageBytes(project.EventImage);

    //     return Document.Create(container =>
    //     {
    //         container.Page(page =>
    //         {
    //             page.Size(PageSizes.A4);
    //             page.Margin(30);
    //             page.DefaultTextStyle(x => x.FontSize(12));

    //             page.Content().Column(col =>
    //             {
    //                 col.Item().Text(project.EventTitle).FontSize(22).SemiBold();

    //                 if (eventImage != null)
    //                 {
    //                     col.Item().PaddingTop(8)
    //                         .Height(160)
    //                         .Image(eventImage)
    //                         .FitArea();
    //                 }

    //                 col.Item().PaddingTop(14).Row(row =>
    //                 {
    //                     row.RelativeItem().Column(left =>
    //                     {
    //                         left.Item().Text($"掲示場所：{spotName}").FontSize(16).SemiBold();
    //                         left.Item().PaddingTop(6).Text(isRequired ? "この場所は【必須スタンプ】です" : "");
    //                         left.Item().PaddingTop(10).Text($"有効期限：{project.ValidFrom:yyyy/MM/dd HH:mm} ～ {project.ValidTo:yyyy/MM/dd HH:mm}");
    //                     });

    //                     row.ConstantItem(180).AlignMiddle().AlignCenter()
    //                         .Border(1).Padding(8)
    //                         .Column(q =>
    //                         {
    //                             q.Item().Image(qrPng).FitArea();
    //                             q.Item().PaddingTop(6).Text("読み取りはこちら").FontSize(10).AlignCenter();
    //                         });
    //                 });

    //                 col.Item().PaddingTop(16).Text("読み取り方法：スマホのカメラでQRを読み取り、表示された画面の指示に従ってください。");

    //                 col.Item().PaddingTop(8).Text("注意：").SemiBold();
    //                 col.Item().Text("・通信が必要です");
    //                 col.Item().Text("・スタンプは全て同じ端末（同じブラウザ）で集めてください");
    //             });
    //         });
    //     }).GeneratePdf();
    // }


    // private byte[] BuildSpotsPosterPdfAllInOne(ProjectDto project)
    // {
    //     var eventImage = GetEventImageBytes(project.EventImage);

    //     return Document.Create(container =>
    //     {
    //         foreach (var spot in project.Spots)
    //         {
    //             var url = $"{project.Urls.ReadStampBase}?e={project.EventId}&s={spot.SpotId}&t={spot.SpotToken}";
    //             _logger.LogInformation("Generating sopt PDF with URL: {Url}", url);
    //             var qrPng = BuildQrPng(url);

    //             container.Page(page =>
    //             {
    //                 page.Size(PageSizes.A4);
    //                 page.Margin(30);
    //                 page.DefaultTextStyle(x => x.FontSize(12));

    //                 page.Content().Column(col =>
    //                 {
    //                     col.Item().Text(project.EventTitle).FontSize(24).SemiBold();

    //                     if (eventImage != null)
    //                     {
    //                         col.Item().PaddingTop(8)
    //                             .Height(160)
    //                             .Image(eventImage)
    //                             .FitArea();
    //                     }

    //                     col.Item().PaddingTop(14).Row(row =>
    //                     {
    //                         row.RelativeItem().Column(left =>
    //                         {
    //                             left.Item().Text($"掲示場所：{spot.SpotName}").FontSize(16).SemiBold();
    //                             left.Item().PaddingTop(6).Text($"有効期限：{project.ValidFrom:yyyy/MM/dd HH:mm} ～ {project.ValidTo:yyyy/MM/dd HH:mm}");
    //                             left.Item().PaddingTop(16).Text(spot.IsRequired ? "この場所は【必須スタンプ】です" : "");
    //                         });

    //                         row.ConstantItem(180).AlignMiddle().AlignCenter()
    //                             .Border(1).Padding(8)
    //                             .Column(q =>
    //                             {
    //                                 q.Item().Image(qrPng).FitArea();
    //                                 q.Item().PaddingTop(6).Text("読み取りはこちら").FontSize(10).AlignCenter();
    //                             });
    //                     });

    //                     col.Item().PaddingTop(16).Text("読み取り方法：スマホのカメラでQRを読み取り、表示された画面の指示に従ってください。");

    //                     col.Item().PaddingTop(8).Text("注意：").SemiBold();
    //                     col.Item().Text("・通信が必要です");
    //                     col.Item().Text("・スタンプは全て同じ端末（同じブラウザ）で集めてください");
    //                 });
    //             });
    //         }
    //     }).GeneratePdf();
    // }
private byte[] BuildSpotsPosterPdfAllInOne(ProjectDto project)
{
    var eventImage = GetEventImageBytes(project.EventImage);
    // 将来：projectにSponsorAdを追加したらここで受ける
    SponsorAdDto? sponsor = null; // project.SponsorAd;

    var hasImage = eventImage != null;
    var qrSize = hasImage ? 240 : 400; // ★ここ調整ポイント

    return Document.Create(container =>
    {
        foreach (var spot in project.Spots)
        {
            var url = $"{project.Urls.ReadStampBase}?e={project.EventId}&s={spot.SpotId}&t={spot.SpotToken}";
            var qrPng = BuildQrPng(url);

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(26);
                page.DefaultTextStyle(x => x.FontSize(12));

                // 背景をほんのり
                page.PageColor(Colors.Grey.Lighten5);

                page.Content().DefaultTextStyle(x => x.FontFamily("Noto Sans JP")).Column(col =>
                {
                    // ===== Header band =====
                    col.Item().Element(e =>
                        HeaderBand(e, project.EventTitle, "", Colors.Blue.Lighten4));
                    // col.Item().Element(e => HeaderBand(e, project.EventTitle));

                    // ===== Main card =====
                    col.Item().PaddingTop(12).Element(card =>
                    {
                        card
                            .Border(1)
                            .BorderColor(Colors.Grey.Lighten2)
                            .Background(Colors.White)
                            .Padding(16)
                            .Column(body =>
                            {
                                // イベント画像（あれば）
                                if (eventImage != null)
                                {
                                    body.Item()
                                        .Height(150)
                                        .Image(eventImage)
                                        .FitArea();

                                    body.Item().PaddingTop(10);
                                }

                                // 掲示場所＋必須バッジ
                                body.Item().Row(r =>
                                {
                                    r.RelativeItem().Column(left =>
                                    {
                                        left.Item().Text("掲示場所").FontSize(11).FontColor(Colors.Grey.Darken2);
                                        left.Item().Text(spot.SpotName).FontSize(20).SemiBold();
                                    });

                                    r.ConstantItem(140).AlignMiddle().AlignRight().Element(b =>
                                    {
                                        if (spot.IsRequired)
                                            Badge(b, "必須スタンプ", PdfBrandColors.BadgePrimary);
                                        else
                                            Badge(b, "任意スタンプ", PdfBrandColors.BadgeSecondary);
                                    });
                                });

                                // 有効期限
                                body.Item().PaddingTop(10)
                                    .Text($"有効期限：{project.ValidFrom:yyyy/MM/dd HH:mm} ～ {project.ValidTo:yyyy/MM/dd HH:mm}")
                                    .FontColor(Colors.Grey.Darken2);

                                // QRエリア（目立たせる）
                                body.Item().PaddingTop(18).AlignCenter().Column(qrCol =>
                                {
                                    qrCol.Item()
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Lighten2)
                                        .Padding(16)
                                        .AlignCenter()
                                        .Width(qrSize)
                                        .Height(qrSize)
                                        .Image(qrPng)
                                        .FitArea();

                                    qrCol.Item()
                                        .PaddingTop(8)
                                        .Text("スマホのカメラで読み取り")
                                        .FontSize(11)
                                        .FontColor(Colors.Grey.Darken1)
                                        .AlignCenter();
                                });


                                // 注意パネル（余白を埋めて“貼る場所”を減らす）
                                body.Item().PaddingTop(14).Element(n => NoticePanel(n));

                                // URL（PC入力用、フッターに小さく）
                                body.Item().PaddingTop(10)
                                    .Hyperlink(url)
                                    .Text(url)
                                    .FontFamily("DejaVu Sans Mono")
                                    .FontSize(9)
                                    .FontColor(Colors.Blue.Medium);
                            });
                    });

                    // ===== Footer =====
                    col.Item().PaddingTop(10).AlignCenter().Text("発行：Qみっけ（q-mikke.com）").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        }
    }).GeneratePdf();
}

private static void Badge(IContainer c, string text, string hexColor)
{
    c.Background(hexColor)
     .PaddingVertical(6)
     .PaddingHorizontal(12)
     .AlignMiddle()
     .AlignCenter()
     .Text(text)
        .FontColor("#FFFFFF")
        .FontSize(11)
        .SemiBold();
}

private static void NoticePanel(IContainer c)
{
    c.Border(1)
     .BorderColor(Colors.Grey.Lighten2)
     .Background(Colors.Grey.Lighten5)
     .Padding(10)
     .Column(col =>
     {
         col.Item().Text("注意").SemiBold();
         Bullet(col, "通信が必要です。");
         Bullet(col, "スタンプは全て同じ端末（同じブラウザ）で集めてください。");
     });

    static void Bullet(ColumnDescriptor col, string text)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(14).Text("・");
            r.RelativeItem().Text(text);
        });
    }
}

private static void SponsorPanel(IContainer c, SponsorAdDto sponsor)
{
    c.Border(1)
     .BorderColor(Colors.Orange.Medium)
     .Background(Colors.Orange.Lighten5)
     .Padding(10)
     .Column(col =>
     {
         col.Item().Text(sponsor.Title ?? "協賛").SemiBold().FontColor(Colors.Orange.Darken3);
         if (!string.IsNullOrWhiteSpace(sponsor.SponsorName))
             col.Item().Text(sponsor.SponsorName).FontSize(12).SemiBold();

         if (sponsor.ImageBytes != null && sponsor.ImageBytes.Length > 0)
             col.Item().PaddingTop(6).Height(60).Image(sponsor.ImageBytes).FitArea();

         if (!string.IsNullOrWhiteSpace(sponsor.Url))
             col.Item().PaddingTop(6).Text(sponsor.Url).FontSize(9).FontColor(Colors.Orange.Darken3);
     });
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
                page.DefaultTextStyle(x => x.FontFamily("Noto Sans JP").FontSize(12));

                page.Content().Column(col =>
                {
                    col.Item().Text("集計画面アクセス用QRコード").FontSize(22).SemiBold();
                    col.Item().Text(project.EventTitle).FontSize(14).FontColor(Colors.Grey.Darken2);

                    if (eventImage != null)
                    {
                        col.Item().PaddingTop(8)
                            .Height(160)
                            .Image(eventImage)
                            .FitArea();
                    }

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

                            r.Item().PaddingTop(10).Text("集計画面URL（PC用）").SemiBold();
                            // r.Item().Text(url).FontSize(10).FontColor(Colors.Grey.Darken2);
                            r.Item().Hyperlink(url)
                                .Text(url)
                                .FontFamily("DejaVu Sans Mono")
                                .FontSize(10)
                                .FontColor(Colors.Blue.Medium);

                            r.Item().PaddingTop(10).Text("パスワード").SemiBold();
                            // r.Item().Text(project.TotalizePassword).FontSize(18).SemiBold();
                            r.Item().Text(project.TotalizePassword).FontFamily("DejaVu Sans Mono").FontSize(18);

                            r.Item().PaddingTop(10).Text($"有効期限：{project.ValidFrom:yyyy/MM/dd HH:mm} ～ {project.ValidTo:yyyy/MM/dd HH:mm}");
                            // r.Item().PaddingTop(8).Text("※ パスワードは後から変更可能（実装予定）").FontColor(Colors.Grey.Darken2);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void HeaderBand(IContainer c, string eventTitle, string purposeLabel, string bandColor)
    {
        c.Background(bandColor)
        .PaddingVertical(10)
        .PaddingHorizontal(14)
        .Row(r =>
        {
            r.RelativeItem().Column(left =>
            {
                left.Item().Text(eventTitle).FontSize(20).SemiBold().FontColor(Colors.Grey.Darken4);
                left.Item().Text(purposeLabel).FontSize(11).FontColor(Colors.Grey.Darken2);
            });

            // r.ConstantItem(110).AlignRight().AlignMiddle()
            // .Text("Qみっけ").FontSize(12).SemiBold().FontColor(Colors.Grey.Darken3);
        });
    }


    private byte[] BuildSimpleQrPdf(
        string title,
        string subtitle,
        string eventTitle,
        DateTime validFrom,
        DateTime validTo,
        string url,
        byte[]? eventImage)
    {
        var qrPng = BuildQrPng(url);

        return Document.Create(container =>
        {
            // =========================
            // Page 1: QR
            // =========================
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(26);
                page.DefaultTextStyle(x => x.FontSize(12));
                page.PageColor(Colors.Grey.Lighten5);

                page.Content().DefaultTextStyle(x => x.FontFamily("Noto Sans JP")).Column(col =>
                {
                    // ヘッダー（ゴール用はオレンジ）
                    col.Item().Element(e =>
                        HeaderBand(e, eventTitle, "主催者用：ゴールQR（掲示しないでください）", Colors.Orange.Lighten4));

                    // メインカード
                    col.Item().PaddingTop(12).Element(card =>
                    {
                        card.Border(1).BorderColor(Colors.Grey.Lighten2)
                            .Background(Colors.White)
                            .Padding(16)
                            .Column(body =>
                            {
                                // 右上バッジで用途を明確化（角丸は使わない版）
                                body.Item().Row(r =>
                                {
                                    r.RelativeItem();
                                    r.ConstantItem(160).AlignRight().Element(b =>
                                        Badge(b, "主催者用 / ゴールQR", PdfBrandColors.BadgeGoal));
                                });

                                // （任意）イベント画像：ゴール用は小さめでもOK
                                if (eventImage != null)
                                {
                                    body.Item().PaddingTop(6)
                                        .Height(120)
                                        .Image(eventImage)
                                        .FitArea();
                                }

                                body.Item().PaddingTop(10)
                                    .Text("ゴール時に、参加者のスマホでこのQRを読み取ってください。")
                                    .FontSize(13)
                                    .SemiBold();

                                body.Item().PaddingTop(10)
                                    .Text($"有効期限：{validFrom:yyyy/MM/dd HH:mm} ～ {validTo:yyyy/MM/dd HH:mm}")
                                    .FontColor(Colors.Grey.Darken2);

                                // QR（大きめ・中央）
                                var qrSize = (eventImage != null) ? 240 : 320;

                                body.Item().PaddingTop(16).AlignCenter().Column(qr =>
                                {
                                    qr.Item()
                                    .Border(2).BorderColor(Colors.Orange.Medium)
                                    .Padding(14)
                                    .Width(qrSize).Height(qrSize)
                                    .AlignCenter()
                                    .Image(qrPng).FitArea();

                                    qr.Item().PaddingTop(8)
                                    .Text("読み取りはこちら")
                                    .FontSize(11)
                                    .FontColor(Colors.Grey.Darken1)
                                    .AlignCenter();
                                });

                                // 注意（掲示用と同じトーン）
                                body.Item().PaddingTop(14).Element(n =>
                                {
                                    n.Border(1).BorderColor(Colors.Grey.Lighten2)
                                    .Background(Colors.Grey.Lighten5)
                                    .Padding(10)
                                    .Column(list =>
                                    {
                                        list.Item().Text("注意").SemiBold();
                                        list.Item().Text("・このQRは主催者が管理してください（掲示しないでください）。");
                                        list.Item().Text("・通信が必要です。");
                                    });
                                });

                                // URLはクリックできるように（デジタル利用も想定）
                                body.Item().PaddingTop(10)
                                    .Hyperlink(url)
                                    .Text(url)
                                    .FontFamily("DejaVu Sans Mono")
                                    .FontSize(9)
                                    .FontColor(Colors.Blue.Medium);
                            });
                    });

                    col.Item().PaddingTop(10).AlignCenter()
                        .Text("発行：Qみっけ（q-mikke.com）")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });
            });


            // =========================
            // Page 2: Organizer guide
            // =========================
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Content().DefaultTextStyle(x => x.FontFamily("Noto Sans JP")).Column(col =>
                {
                    col.Item().Text("ゴール運用手順（主催者向け）").FontSize(20).SemiBold();

                    col.Item().PaddingTop(8).Text(eventTitle).FontSize(14).FontColor(Colors.Grey.Darken2);

                    col.Item().PaddingTop(14).Text("スタンプラリーの流れ").FontSize(14).SemiBold();

                    col.Item().PaddingTop(8).Column(list =>
                    {
                        void Step(string n, string text)
                        {
                            list.Item().Row(r =>
                            {
                                r.ConstantItem(22).Text($"{n}.").SemiBold();
                                r.RelativeItem().Text(text);
                            });
                        }
                        void SubStep(string n, string text)
                        {
                            list.Item().Row(r =>
                            {
                                r.ConstantItem(22).Text($"  ").SemiBold();
                                r.RelativeItem().Text(text);
                            });
                        }

                        Step("1", "参加者が掲示場所のQRを読み取り、ゴール条件を満たすようにスタンプを集めます。");
                        SubStep("", "【必須スタンプ】は全て集めなければゴールできません");
                        SubStep("", "【必須スタンプ】以外のゴール条件は主催者側で決めてください（例：任意スタンプ3個以上など）。");
                        Step("2", "ゴールした参加者を主催者の受付へ案内し、この「ゴールQR」を読み取ってもらいます。");
                        Step("3", "画面に「ゴール前です」と表示されていることを主催者が確認します。");
                        Step("4", "参加者本人に「ゴールする」ボタンを押してもらいます（主催者の確認のもと）。");
                        Step("5", "画面が「ゴール済みです」となったことを主催者が確認し、景品等をお渡しください。");

                    });
                    {
                        void Bullet(IContainer container, string text)
                        {
                            container.Row(row =>
                            {
                                row.ConstantItem(16).Text("・"); // 記号部分の幅を固定
                                row.RelativeItem().Text(text);
                            });
                        }
                        col.Item().PaddingTop(16).Text("注意").FontSize(14).SemiBold();
                        col.Item().PaddingTop(6).Element(c =>
                            Bullet(c, "ゴールすると以後その端末ではスタンプ読み取りができません。"));
                        col.Item().Element(c =>
                            Bullet(c, "スタンプは全て同じ端末（同じブラウザ）で集めてください。"));
                        col.Item().Element(c =>
                            Bullet(c, "本サービスは簡易的な仕組みのため、状況によっては同一人物が複数回ゴールできる場合があります。厳密な管理が必要な場合は、主催者様にて氏名確認等の対応をお願いいたします。"));
                    }

                    col.Item().PaddingTop(14).Text("有効期限").FontSize(14).SemiBold();
                    col.Item().Text($"{validFrom:yyyy/MM/dd HH:mm} ～ {validTo:yyyy/MM/dd HH:mm}");

                    // 任意：PC用URL表示（受付PCで開きたい場合など）
                    col.Item().PaddingTop(14).Text("ゴール画面URL").FontSize(14).SemiBold();
                    // col.Item().Text(url).FontSize(10).FontColor(Colors.Grey.Darken2);
                    col.Item().Hyperlink(url)
                        .Text(url)
                        .FontFamily("DejaVu Sans Mono")
                        .FontSize(10)
                        .FontColor(Colors.Blue.Medium);
                });
            });
        }).GeneratePdf();
    }


    private byte[] BuildProjectPackageQmkpj(ProjectDto project)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 画像を内側ZIPに格納し、manifestに参照情報を持たせる
            EventImageRef? imageRef = null;

            if (project.EventImage != null && !string.IsNullOrWhiteSpace(project.EventImage.Base64))
            {
                var imgBytes = GetEventImageBytes(project.EventImage);

                if (imgBytes != null && imgBytes.Length > 0)
                {
                    var ext = GuessImageExtension(project.EventImage.ContentType, project.EventImage.FileName);
                    var imagePathInZip = $"images/event{ext}";

                    AddBytes(zip, imagePathInZip, imgBytes);

                    imageRef = new EventImageRef
                    {
                        FileName = imagePathInZip,
                        ContentType = project.EventImage.ContentType ?? "application/octet-stream",
                        SizeBytes = imgBytes.LongLength,
                        Sha256 = ComputeSha256Hex(imgBytes)
                    };
                }
            }

            // project.json（最小 + 画像参照）
            var export = new StampRallyProjectExport
            {
                App = "Qmikke",
                Format = "stamp-rally-project",
                Version = 1,

                EventTitle = project.EventTitle,
                ValidFrom = project.ValidFrom,
                ValidTo = project.ValidTo,

                EventImage = imageRef,

                Spots = project.Spots.Select(s => new SpotExport
                {
                    SpotName = s.SpotName,
                    IsRequired = s.IsRequired
                }).ToList(),
                CheckCode = null,
            };
            var payload = JsonSerializer.Serialize(export, ProjectSignatureService.SignJsonOptions);
            var secret = _config["ProjectExport:HmacSecret"]!;
            export.CheckCode = ProjectSignatureService.ComputeHmacBase64Url(secret, payload);

            var json = JsonSerializer.Serialize(export, JsonOptions());
            AddText(zip, "project.json", json);
        }

        return ms.ToArray();
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

    private static string ComputeSha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GuessImageExtension(string? contentType, string? originalFileName)
    {
        var ct = (contentType ?? "").ToLowerInvariant();
        if (ct == "image/jpeg" || ct == "image/jpg") return ".jpg";
        if (ct == "image/png") return ".png";
        if (ct == "image/webp") return ".webp";
        if (ct == "image/gif") return ".gif";

        var ext = Path.GetExtension(originalFileName ?? "");
        if (string.IsNullOrWhiteSpace(ext)) return ".img";

        ext = ext.ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        if (allowed.Contains(ext))
            return ext == ".jpeg" ? ".jpg" : ext;

        return ".img";
    }

    private void SaveLocalFile(byte[] zipBytes)
    {
        var now = DateTime.Now;
        var timestamp = now.ToString("yyyyMMddHHmmss");

        // 4桁ランダム（0000〜9999）
        var randomNumber = RandomNumberGenerator.GetInt32(0, 10000);
        var randomPart = randomNumber.ToString("D4");

        var fileName = $"{timestamp}_{randomPart}.zip";

        // 保存先ディレクトリ（例：App_Data/archives）
        var saveDir = Path.Combine(Directory.GetCurrentDirectory(), "archives");

        if (!Directory.Exists(saveDir))
            Directory.CreateDirectory(saveDir);

        var fullPath = Path.Combine(saveDir, fileName);

        System.IO.File.WriteAllBytes(fullPath, zipBytes);
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

        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        [Required]
        public DateTime ValidFrom { get; set; }

        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        [Required]
        public DateTime ValidTo { get; set; }

        public string ValidFromMin = "";
        public string ValidFromMax = "";
        public string ValidToMin = "";
        public string ValidToMax = "";

        public IFormFile? EventImageFile { get; set; }

        // ★ZIPインポートで復元した画像を保持（アップロードが無い場合に採用）
        public EventImageDto? LoadedEventImage { get; set; }

        public List<SpotInputModel> Spots { get; set; } = new() { new SpotInputModel() };

        public string? LoadToken { get; set; }

        public bool AgreeToTerms { get; set; }
    }

    public class SpotInputModel
    {
        public string SpotName { get; set; } = "";
        public bool IsRequired { get; set; }
    }

    private static class PdfBrandColors
    {
        public const string BadgePrimary = "#2563EB";   // 必須
        public const string BadgeSecondary = "#FB923C"; // 任意
        public const string BadgeGoal = "0f0f0f";
    }
}

