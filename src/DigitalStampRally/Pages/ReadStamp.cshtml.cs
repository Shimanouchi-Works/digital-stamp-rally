using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DigitalStampRally.Services;

namespace DigitalStampRally.Pages;

[IgnoreAntiforgeryToken] // ReadStampは匿名＆Cookie前提ではないので簡略化（必要なら後で強化）
public class ReadStampModel : PageModel
{
    private readonly IProjectStore _projectStore;
    private readonly IStampLogStore _logStore;
    private readonly IAchievementStore _achievementStore;
    private readonly IGoalStore _goalStore;

    public ReadStampModel(
                IProjectStore projectStore,
                IStampLogStore logStore,
                IAchievementStore achievementStore,
                IGoalStore goalStore)
    {
        _projectStore = projectStore;
        _logStore = logStore;
        _achievementStore = achievementStore;
        _goalStore = goalStore;
    }

    // 画面表示用
    public string EventId { get; private set; } = "";
    public string SpotId { get; private set; } = "";
    public string SpotName { get; private set; } = "";
    public string EventTitle { get; private set; } = "";
    public DateTime ValidFrom { get; private set; }
    public DateTime ValidTo { get; private set; }

    public List<string> RequiredSpotIds { get; private set; } = new();

    public Dictionary<string, SpotMeta> SpotMap { get; private set; } = new();

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet(string? e, string? s, string? t)
    {
        // 必須パラメータ
        if (string.IsNullOrWhiteSpace(e) || string.IsNullOrWhiteSpace(s) || string.IsNullOrWhiteSpace(t))
        {
            ErrorMessage = "QRコードの情報が不足しています。";
            return Page();
        }

        // プロジェクト取得
        if (!_projectStore.TryGet(e, out var project))
        {
            ErrorMessage = "このイベントは見つかりませんでした（期限切れ、または未登録の可能性があります）。";
            return Page();
        }

        // 有効期限チェック
        var now = DateTime.Now;
        if (now < project.ValidFrom || now > project.ValidTo)
        {
            ErrorMessage = "このQRコードは有効期限外です。";
            return Page();
        }

        // spot検索
        var spot = project.Spots.FirstOrDefault(x => x.SpotId == s);
        if (spot == null)
        {
            ErrorMessage = "掲示場所が見つかりませんでした。";
            return Page();
        }

        // トークン検証（MVP：一致チェック）
        if (!string.Equals(spot.SpotToken, t, StringComparison.Ordinal))
        {
            ErrorMessage = "QRコードが無効です（トークン不一致）。";
            return Page();
        }

        // 表示用セット
        EventId = project.EventId;
        SpotId = spot.SpotId;
        SpotName = spot.SpotName;
        EventTitle = project.EventTitle;
        ValidFrom = project.ValidFrom;
        ValidTo = project.ValidTo;

        RequiredSpotIds = project.Spots.Where(x => x.IsRequired).Select(x => x.SpotId).ToList();

        SpotMap = project.Spots.ToDictionary(
            x => x.SpotId,
            x => new SpotMeta { Name = x.SpotName, IsRequired = x.IsRequired }
        );

        return Page();
    }

    // ---- Ajax: ログ保存 ----
    public async Task<IActionResult> OnPostLogAsync([FromBody] LogRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.EventId) || string.IsNullOrWhiteSpace(req.SpotId) || string.IsNullOrWhiteSpace(req.VisitorId))
            return new JsonResult(new { success = false });

        // プロジェクト存在チェック（無いなら捨てる）
        if (!_projectStore.TryGet(req.EventId, out var project))
            return new JsonResult(new { success = false });

        // 期限外なら捨てる
        var now = DateTime.Now;
        if (now < project.ValidFrom || now > project.ValidTo)
            return new JsonResult(new { success = false });

        await _logStore.AppendStampLogAsync(new StampLog
        {
            At = DateTime.Now,
            EventId = req.EventId,
            SpotId = req.SpotId,
            VisitorId = req.VisitorId,
            IsDuplicate = req.IsDuplicate,
            UserAgent = req.UserAgent ?? ""
        });

        return new JsonResult(new { success = true });
    }

    // ---- Ajax: 達成コード発行/取得 ----
    public async Task<IActionResult> OnPostAchievementAsync([FromBody] AchievementRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.EventId) || string.IsNullOrWhiteSpace(req.VisitorId))
            return new JsonResult(new { success = false, message = "リクエスト不正" });

        if (!_projectStore.TryGet(req.EventId, out var project))
            return new JsonResult(new { success = false, message = "イベント不明" });

        var now = DateTime.Now;
        if (now < project.ValidFrom || now > project.ValidTo)
            return new JsonResult(new { success = false, message = "期限外" });

        // 必須条件チェック（MVP：クライアント申告のspotIdsで判定）
        var required = project.Spots.Where(x => x.IsRequired).Select(x => x.SpotId).ToHashSet();
        var collected = (req.CollectedSpotIds ?? new List<string>()).ToHashSet();

        if (required.Count > 0 && !required.All(collected.Contains))
            return new JsonResult(new { success = false, message = "まだ条件を満たしていません" });

        // 既存コードがあればそれを返す（いつでも確認できる）
        var code = await _achievementStore.GetOrCreateAsync(req.EventId, req.VisitorId);

        return new JsonResult(new { success = true, code });
    }

    public async Task<IActionResult> OnPostGoalStatusAsync([FromBody] GoalStatusRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.EventId) || string.IsNullOrWhiteSpace(req.VisitorId))
            return new JsonResult(new { success = false });

        var goal = await _goalStore.GetAsync(req.EventId, req.VisitorId);
        return new JsonResult(new { success = true, goaled = (goal != null) });
    }

    // ---- request models ----
    public class LogRequest
    {
        public string VisitorId { get; set; } = "";
        public string EventId { get; set; } = "";
        public string SpotId { get; set; } = "";
        public bool IsDuplicate { get; set; }
        public string? UserAgent { get; set; }
    }

    public class AchievementRequest
    {
        public string VisitorId { get; set; } = "";
        public string EventId { get; set; } = "";
        public List<string>? CollectedSpotIds { get; set; }
    }

    public class SpotMeta
    {
        public string Name { get; set; } = "";
        public bool IsRequired { get; set; }
    }

    public class GoalStatusRequest
    {
        public string EventId { get; set; } = "";
        public string VisitorId { get; set; } = "";
    }
}

