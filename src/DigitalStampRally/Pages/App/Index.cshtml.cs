using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DigitalStampRally.Services;
using DigitalStampRally.Database;

namespace DigitalStampRally.Pages;

public class IndexModel : PageModel
{
    private readonly IProjectDraftStore _draftStore;
    private readonly DigitalStampRallyContext _db;

    public IndexModel(IProjectDraftStore draftStore, DigitalStampRallyContext db)
    {
        _draftStore = draftStore;
        _db = db;
    }

    [BindProperty]
    public string? ErrorMessage { get; set; }

    // 任意：DB上のイベント一覧を表示したい場合に使う
    public List<EventSummary> RecentEvents { get; private set; } = new();

    public string? LoadedToken { get; set; }

    public async Task OnGetAsync(string? loaded=null)
    {
        try
        {
            LoadedToken = loaded;

            // IndexでDBを触るのは必須ではありませんが、
            // “DB対応”として「最近のイベント」を表示できるようにしておくと運用が楽です。
            // 公開中(1)を優先して新しい順に20件表示。
            RecentEvents = await _db.Events
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new EventSummary
                {
                    Id = e.Id,
                    Title = e.Title,
                    Status = e.Status,
                    StartsAt = e.StartsAt,
                    EndsAt = e.EndsAt,
                    CreatedAt = e.CreatedAt
                })
                .Take(20)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorMessage = "不明なエラー";
        }
    }

    // ★ これは仕様上必要なので残す（JSONロード → CreateNewへ）
    public async Task<IActionResult> OnPostLoadAsync(IFormFile? projectFile)
    {
        try
        {
            // 基本バリデーション
            if (projectFile == null || projectFile.Length == 0)
            {
                ErrorMessage = "JSONファイルが選択されていません。";
                await OnGetAsync(); // 一覧を表示している場合、再表示に必要
                return Page();
            }

            // 初期リリース想定：10MB制限（必要なら後で調整）
            const long maxBytes = 10 * 1024 * 1024;
            if (projectFile.Length > maxBytes)
            {
                ErrorMessage = "ファイルサイズが大きすぎます（最大 10MB）。";
                await OnGetAsync();
                return Page();
            }

            // 拡張子チェック（最低限）
            var ext = Path.GetExtension(projectFile.FileName);
            if (!string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "拡張子が .json のファイルを選択してください。";
                await OnGetAsync();
                return Page();
            }

            string json;
            try
            {
                using var sr = new StreamReader(projectFile.OpenReadStream());
                json = await sr.ReadToEndAsync();
            }
            catch
            {
                ErrorMessage = "ファイルの読み取りに失敗しました。";
                await OnGetAsync();
                return Page();
            }

            // “JSONっぽい”最低限の検査（厳密な検証はCreateNew側で行う）
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
            {
                ErrorMessage = "JSON形式として読み取れませんでした。";
                await OnGetAsync();
                return Page();
            }

            // サーバー側に短時間だけ保持して、CreateNewへトークンで渡す
            var token = _draftStore.Save(json);

            return Redirect($"/App/CreateNew?load={Uri.EscapeDataString(token)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorMessage = "不明なエラー";
            return Page();
        }
    }

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
