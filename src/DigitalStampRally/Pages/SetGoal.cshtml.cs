using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DigitalStampRally.Services;
using DigitalStampRally.Models;

namespace DigitalStampRally.Pages;

[IgnoreAntiforgeryToken]
public class SetGoalModel : PageModel
{
    private readonly IProjectStore _projectStore;
    private readonly IGoalStore _goalStore;
    private readonly IAchievementStore _achievementStore;

    public SetGoalModel(IProjectStore projectStore, IGoalStore goalStore, IAchievementStore achievementStore)
    {
        _projectStore = projectStore;
        _goalStore = goalStore;
        _achievementStore = achievementStore;
    }

    public string EventId { get; private set; } = "";
    public string EventTitle { get; private set; } = "";
    public DateTime ValidFrom { get; private set; }
    public DateTime ValidTo { get; private set; }

    public List<string> RequiredSpotIds { get; private set; } = new();

    // JSで spotId -> name/isRequired を引くためのマップ
    public Dictionary<string, SpotMeta> SpotMap { get; private set; } = new();

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet(string? e, string? t)
    {
        if (string.IsNullOrWhiteSpace(e) || string.IsNullOrWhiteSpace(t))
        {
            ErrorMessage = "QRコードの情報が不足しています。";
            return Page();
        }

        if (!_projectStore.TryGet(e, out var project))
        {
            ErrorMessage = "このイベントは見つかりませんでした。";
            return Page();
        }

        var now = DateTime.Now;
        if (now < project.ValidFrom || now > project.ValidTo)
        {
            ErrorMessage = "このQRコードは有効期限外です。";
            return Page();
        }

        if (!string.Equals(project.GoalToken, t, StringComparison.Ordinal))
        {
            ErrorMessage = "QRコードが無効です（トークン不一致）。";
            return Page();
        }

        EventId = project.EventId;
        EventTitle = project.EventTitle;
        ValidFrom = project.ValidFrom;
        ValidTo = project.ValidTo;

        RequiredSpotIds = project.Spots.Where(x => x.IsRequired).Select(x => x.SpotId).ToList();
        SpotMap = project.Spots.ToDictionary(
            s => s.SpotId,
            s => new SpotMeta { Name = s.SpotName, IsRequired = s.IsRequired }
        );

        return Page();
    }

    // ----- Ajax: ゴール済み状態確認 -----
    public async Task<IActionResult> OnPostStatusAsync([FromBody] StatusRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.EventId) || string.IsNullOrWhiteSpace(req.VisitorId))
            return new JsonResult(new { success = false });

        var goal = await _goalStore.GetAsync(req.EventId, req.VisitorId);
        if (goal == null)
            return new JsonResult(new { success = true, goaled = false });

        return new JsonResult(new { success = true, goaled = true, code = goal.AchievementCode });
    }

    // ----- Ajax: ゴール確定 -----
    public async Task<IActionResult> OnPostGoalAsync([FromBody] GoalRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.EventId) || string.IsNullOrWhiteSpace(req.VisitorId))
            return new JsonResult(new { success = false, message = "リクエスト不正" });

        if (!_projectStore.TryGet(req.EventId, out var project))
            return new JsonResult(new { success = false, message = "イベント不明" });

        var now = DateTime.Now;
        if (now < project.ValidFrom || now > project.ValidTo)
            return new JsonResult(new { success = false, message = "期限外" });

        // 既にゴール済みならその情報を返す（多重押下/再アクセス対策）
        var existing = await _goalStore.GetAsync(req.EventId, req.VisitorId);
        if (existing != null)
            return new JsonResult(new { success = true, code = existing.AchievementCode });

        // 必須条件チェック（クライアント申告の collectedSpotIds を使うMVP）
        var required = project.Spots.Where(x => x.IsRequired).Select(x => x.SpotId).ToHashSet();
        var collected = (req.CollectedSpotIds ?? new List<string>()).ToHashSet();

        if (required.Count > 0 && !required.All(collected.Contains))
            return new JsonResult(new { success = false, message = "必須スタンプが不足しています" });

        // 達成コード（ReadStampでも使う「いつでも確認できる」もの）
        var code = await _achievementStore.GetOrCreateAsync(req.EventId, req.VisitorId);

        await _goalStore.SetAsync(new GoalRecord
        {
            EventId = req.EventId,
            VisitorId = req.VisitorId,
            GoaledAt = DateTime.Now,
            AchievementCode = code,
            CollectedSpotIds = collected.ToList()
        });

        return new JsonResult(new { success = true, code });
    }

    // ---- request models ----
    public class StatusRequest
    {
        public string EventId { get; set; } = "";
        public string VisitorId { get; set; } = "";
    }

    public class GoalRequest
    {
        public string EventId { get; set; } = "";
        public string VisitorId { get; set; } = "";
        public List<string>? CollectedSpotIds { get; set; }
    }

    public class SpotMeta
    {
        public string Name { get; set; } = "";
        public bool IsRequired { get; set; }
    }
}
