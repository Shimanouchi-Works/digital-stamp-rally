using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DigitalStampRally.Services;
using DigitalStampRally.Database;
using DigitalStampRally.Models;

namespace DigitalStampRally.Pages;

public class AppIndexModel : PageModel
{
    private readonly IProjectDraftStore _draftStore;
    private readonly DigitalStampRallyContext _db;
    private readonly IConfiguration _config;

    public AppIndexModel(
                IProjectDraftStore draftStore,
                DigitalStampRallyContext db,
                IConfiguration config
                )
    {
        _draftStore = draftStore;
        _db = db;
        _config = config;
    }

    [BindProperty]
    public string? ErrorMessage { get; set; }

    // 任意：DB上のイベント一覧を表示したい場合に使う
    // public List<EventSummary> RecentEvents { get; private set; } = new();

    // public string? LoadedToken { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            await Task.CompletedTask;
            // LoadedToken = loaded;

            // IndexでDBを触るのは必須ではありませんが、
            // “DB対応”として「最近のイベント」を表示できるようにしておくと運用が楽です。
            // 公開中(1)を優先して新しい順に20件表示。
            // RecentEvents = await _db.Events
            //     .OrderByDescending(e => e.CreatedAt)
            //     .Select(e => new EventSummary
            //     {
            //         Id = e.Id,
            //         Title = e.Title,
            //         Status = e.Status,
            //         StartsAt = e.StartsAt,
            //         EndsAt = e.EndsAt,
            //         CreatedAt = e.CreatedAt
            //     })
            //     .Take(20)
            //     .ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorMessage = "不明なエラー";
        }
    }

    // ★ これは仕様上必要なので残す（JSONロード → CreateNewへ）
    // public async Task<IActionResult> OnPostLoadAsync(IFormFile? projectFile)
    // {
    //     try
    //     {
    //         // 基本バリデーション
    //         if (projectFile == null || projectFile.Length == 0)
    //         {
    //             ErrorMessage = "JSONファイルが選択されていません。";
    //             await OnGetAsync(); // 一覧を表示している場合、再表示に必要
    //             return Page();
    //         }

    //         // 初期リリース想定：10MB制限（必要なら後で調整）
    //         const long maxBytes = 10 * 1024 * 1024;
    //         if (projectFile.Length > maxBytes)
    //         {
    //             ErrorMessage = "ファイルサイズが大きすぎます（最大 10MB）。";
    //             await OnGetAsync();
    //             return Page();
    //         }

    //         // 拡張子チェック（最低限）
    //         var ext = Path.GetExtension(projectFile.FileName);
    //         if (!string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
    //         {
    //             ErrorMessage = "拡張子が .json のファイルを選択してください。";
    //             await OnGetAsync();
    //             return Page();
    //         }

    //         string json;
    //         try
    //         {
    //             using var sr = new StreamReader(projectFile.OpenReadStream());
    //             json = await sr.ReadToEndAsync();
    //         }
    //         catch
    //         {
    //             ErrorMessage = "ファイルの読み取りに失敗しました。";
    //             await OnGetAsync();
    //             return Page();
    //         }

    //         // “JSONっぽい”最低限の検査（厳密な検証はCreateNew側で行う）
    //         if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
    //         {
    //             ErrorMessage = "JSON形式として読み取れませんでした。";
    //             await OnGetAsync();
    //             return Page();
    //         }

    //         // サーバー側に短時間だけ保持して、CreateNewへトークンで渡す
    //         var token = _draftStore.Save(json);

    //         return Redirect($"/App/CreateNew?load={Uri.EscapeDataString(token)}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine(ex);
    //         ErrorMessage = "不明なエラー";
    //         return Page();
    //     }
    // }

    public async Task<IActionResult> OnPostLoadAsync(IFormFile? projectFile)
    {
        try
        {
            if (projectFile == null || projectFile.Length == 0)
            {
                ErrorMessage = "ファイルが選択されていません。";
                await OnGetAsync();
                return Page();
            }

            const long maxBytes = 30 * 1024 * 1024; // 30MB
            if (projectFile.Length > maxBytes)
            {
                ErrorMessage = "ファイルサイズが大きすぎます。";
                await OnGetAsync();
                return Page();
            }

            var ext = Path.GetExtension(projectFile.FileName);

            if (ext.Equals(".qmkpj", StringComparison.OrdinalIgnoreCase))
            {
                // 直 qmkpj
                await using var input = projectFile.OpenReadStream();
                return await LoadFromQmkpjStreamAsync(input);
            }

            if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                // 外側ZIP（PDF付き）から project.qmkpj を探す
                await using var input = projectFile.OpenReadStream();
                using var outer = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);

                var qmkpjEntry = outer.GetEntry(AppConst.QmkpjFileName);
                if (qmkpjEntry == null)
                {
                    ErrorMessage = "ファイル形式が正しくありません(0001)。";//"project.qmkpj が見つかりません。古い形式のZIP、またはこのアプリのファイルではない可能性があります。";
                    await OnGetAsync();
                    return Page();
                }

                await using var qmkpjStream = qmkpjEntry.Open();
                return await LoadFromQmkpjStreamAsync(qmkpjStream);
            }

            ErrorMessage = "ファイル形式が正しくありません(0002)。";//"拡張子が .zip または .qmkpj のファイルを選択してください。";
            await OnGetAsync();
            return Page();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorMessage = "不明なエラー(001)";
            return Page();
        }
    }

    private async Task<IActionResult> LoadFromQmkpjStreamAsync(Stream qmkpjStream)
    {
        string json;
        StampRallyProjectExport manifest;

        byte[]? imageBytes = null;
        string? imageFileName = null;
        string? imageContentType = null;

        try
        {
            using var zip = new ZipArchive(qmkpjStream, ZipArchiveMode.Read, leaveOpen: true);

            var jsonEntry = zip.GetEntry("project.json");
            if (jsonEntry == null)
            {
                ErrorMessage = "ファイル形式が正しくありません(0003)。";//"project.json が見つかりません。このアプリの project.qmkpj ではない可能性があります。";
                await OnGetAsync();
                return Page();
            }

            await using (var es = jsonEntry.Open())
            using (var sr = new StreamReader(es))
            {
                json = await sr.ReadToEndAsync();
            }

            manifest = JsonSerializer.Deserialize<StampRallyProjectExport>(json, JsonOptions())
                    ?? throw new Exception("project.json parse failed");

            var checkCode = manifest.CheckCode;
            manifest.CheckCode = null;
            var payload = JsonSerializer.Serialize(manifest, ProjectSignatureService.SignJsonOptions);
            var secret = _config["ProjectExport:HmacSecret"]!;
            var expected = ProjectSignatureService.ComputeHmacBase64Url(secret, payload);
            if (checkCode == null || !ProjectSignatureService.SecureEquals(checkCode, expected))
            {
                //throw new Exception("project.json が改ざんされています。");
                ErrorMessage = "ファイル形式が正しくありません(0004)。";//"このアプリが出力したプロジェクトファイルではありません。";
                await OnGetAsync();
                return Page();
            }
            


            if (!string.Equals(manifest.App, "Qmikke", StringComparison.Ordinal) ||
                !string.Equals(manifest.Format, "stamp-rally-project", StringComparison.Ordinal) ||
                manifest.Version != 1)
            {
                ErrorMessage = "ファイル形式が正しくありません(0005)。";//"このアプリが出力したプロジェクトファイルではありません。";
                await OnGetAsync();
                return Page();
            }

            // 画像があれば読む + sha256検証
            if (manifest.EventImage != null && !string.IsNullOrWhiteSpace(manifest.EventImage.FileName))
            {
                var imgEntry = zip.GetEntry(manifest.EventImage.FileName);
                if (imgEntry == null)
                {
                    ErrorMessage = "ファイル形式が正しくありません(0006)。";//"イベント画像が project.qmkpj 内に見つかりません（欠損の可能性があります）。";
                    await OnGetAsync();
                    return Page();
                }

                if (imgEntry.Length > DigitalStampRally.Models.AppConst.MaxEventImageBytes)
                {
                    ErrorMessage = $"イベント画像が大きすぎます（最大 {DigitalStampRally.Models.AppConst.MaxEventImageBytes / 1024 / 1024}MB）。";
                    await OnGetAsync();
                    return Page();
                }

                await using var imgStream = imgEntry.Open();
                using var ms = new MemoryStream();
                await imgStream.CopyToAsync(ms);
                imageBytes = ms.ToArray();

                var sha = ComputeSha256Hex(imageBytes);
                if (!string.Equals(sha, manifest.EventImage.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "ファイル形式が正しくありません(0007)。";//"イベント画像の整合性チェックに失敗しました（改ざん/破損の可能性があります）。";
                    await OnGetAsync();
                    return Page();
                }

                imageFileName = Path.GetFileName(manifest.EventImage.FileName);
                imageContentType = manifest.EventImage.ContentType;
            }
        }
        catch
        {
            ErrorMessage = "不明なエラー(001)";//"ファイルの読み取りに失敗しました。ファイルが壊れている可能性があります。";
            await OnGetAsync();
            return Page();
        }

        DraftImagePayload? imagePayload = null;
        if (imageBytes != null)
        {
            imagePayload = new DraftImagePayload
            {
                Bytes = imageBytes,
                FileName = imageFileName ?? "event",
                ContentType = string.IsNullOrWhiteSpace(imageContentType) ? "application/octet-stream" : imageContentType
            };
        }

        var token = _draftStore.Save(json, imagePayload);
        return Redirect($"/App/CreateNew?load={Uri.EscapeDataString(token)}");
    }


    private static string ComputeSha256Hex(byte[] bytes)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static JsonSerializerOptions JsonOptions()
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };



    public class EventSummary
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public int Status { get; set; }
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
