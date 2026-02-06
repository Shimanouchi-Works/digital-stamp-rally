using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DigitalStampRally.Services;

namespace DigitalStampRally.Pages;

[IgnoreAntiforgeryToken]
public class SetGoalModel : PageModel
{
    private readonly DbEventService _eventService;
    private readonly DbStampService _stampService;

    public SetGoalModel(DbEventService eventService, DbStampService stampService)
    {
        _eventService = eventService;
        _stampService = stampService;
    }

    public long EventId { get; private set; }
    public string Token { get; private set; } = ""; // JSのPOSTでも再検証に使う

    public string EventTitle { get; private set; } = "";
    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }

    public List<long> RequiredSpotIds { get; private set; } = new();

    // JSで spotId -> name/isRequired を引くためのマップ
    public Dictionary<long, SpotMeta> SpotMap { get; private set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(long? e, string? t)
    {
        if (e == null || string.IsNullOrWhiteSpace(t))
        {
            ErrorMessage = "QRコードの情報が不足しています。";
            return Page();
        }

        EventId = e.Value;
        Token = t;

        var ev = await _eventService.GetEventAsync(EventId);
        if (ev == null)
        {
            ErrorMessage = "このイベントは見つかりませんでした。";
            return Page();
        }

        // 期限チェック
        var now = DateTime.Now;
        if (ev.StartsAt != null && now < ev.StartsAt)
        {
            ErrorMessage = "このQRコードは有効期限外です（開始前）。";
            return Page();
        }
        if (ev.EndsAt != null && now > ev.EndsAt)
        {
            ErrorMessage = "このQRコードは有効期限外です（終了後）。";
            return Page();
        }

        // ゴールトークン検証（hash照合）
        var ok = await _eventService.ValidateGoalTokenAsync(EventId, Token);
        if (!ok)
        {
            ErrorMessage = "QRコードが無効です。";
            return Page();
        }

        EventTitle = ev.Title;
        ValidFrom = ev.StartsAt;
        ValidTo = ev.EndsAt;

        var required = await _eventService.GetRequiredSpotIdsAsync(EventId);
        RequiredSpotIds = required.ToList();

        var spots = await _eventService.GetActiveSpotsAsync(EventId);
        SpotMap = spots.ToDictionary(
            s => s.Id,
            s => new SpotMeta { Name = s.Name, IsRequired = required.Contains(s.Id) }
        );

        return Page();
    }

    // ----- Ajax: ゴール済み状態確認（goals.goaled_at） -----
    public async Task<IActionResult> OnPostStatusAsync([FromBody] StatusRequest req)
    {
        if (req == null || req.EventId <= 0 || string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.VisitorId))
            return new JsonResult(new { success = false });

        // 直叩き対策：token再検証
        if (!await _eventService.ValidateGoalTokenAsync(req.EventId, req.Token))
            return new JsonResult(new { success = false });

        var ua = Request.Headers.UserAgent.ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        var ipHash = CryptoUtil.Sha256Hex(ip);

        var session = await _stampService.GetOrCreateSessionAsync(req.EventId, req.VisitorId, ua, ipHash);

        // ゴール済みなら code も返す（存在する前提）
        var goaled = await _stampService.IsGoaledAsync(req.EventId, session.Id);
        if (!goaled)
            return new JsonResult(new { success = true, goaled = false });

        // ゴール済み時：EnsureGoaledAsync は同じコードを返すので安全
        var code = await _stampService.EnsureGoaledAsync(req.EventId, session.Id);
        return new JsonResult(new { success = true, goaled = true, code });
    }

    // ----- Ajax: ゴール確定 -----
    public async Task<IActionResult> OnPostGoalAsync([FromBody] GoalRequest req)
    {
        if (req == null || req.EventId <= 0 || string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.VisitorId))
            return new JsonResult(new { success = false, message = "リクエスト不正" });

        // token再検証
        if (!await _eventService.ValidateGoalTokenAsync(req.EventId, req.Token))
            return new JsonResult(new { success = false, message = "無効なQRです" });

        var ev = await _eventService.GetEventAsync(req.EventId);
        if (ev == null)
            return new JsonResult(new { success = false, message = "イベント不明" });

        // 期限チェック
        var now = DateTime.Now;
        if (ev.StartsAt != null && now < ev.StartsAt) return new JsonResult(new { success = false, message = "期限外（開始前）" });
        if (ev.EndsAt != null && now > ev.EndsAt) return new JsonResult(new { success = false, message = "期限外（終了後）" });

        var ua = Request.Headers.UserAgent.ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        var ipHash = CryptoUtil.Sha256Hex(ip);

        var session = await _stampService.GetOrCreateSessionAsync(req.EventId, req.VisitorId, ua, ipHash);

        if (session.IsBlocked ?? false)
            return new JsonResult(new { success = false, message = "ブロックされています" });

        // 既にゴール済みならその情報を返す
        if (await _stampService.IsGoaledAsync(req.EventId, session.Id))
        {
            var code0 = await _stampService.EnsureGoaledAsync(req.EventId, session.Id);
            return new JsonResult(new { success = true, code = code0 });
        }

        // 必須条件チェック：DB stamps で判定（クライアント申告は信用しない）
        var required = await _eventService.GetRequiredSpotIdsAsync(req.EventId);
        if (required.Count > 0)
        {
            var stamped = await _stampService.GetStampedSpotIdsAsync(req.EventId, session.Id);
            if (!required.All(stamped.Contains))
                return new JsonResult(new { success = false, message = "必須スタンプが不足しています" });
        }

        // ゴール確定：goals.goaled_at を埋める（達成コードも返す）
        var code = await _stampService.EnsureGoaledAsync(req.EventId, session.Id);
        return new JsonResult(new { success = true, code });
    }

    // ---- request models ----
    public class StatusRequest
    {
        public long EventId { get; set; }
        public string Token { get; set; } = "";
        public string VisitorId { get; set; } = "";
    }

    public class GoalRequest
    {
        public long EventId { get; set; }
        public string Token { get; set; } = "";
        public string VisitorId { get; set; } = "";
    }

    public class SpotMeta
    {
        public string Name { get; set; } = "";
        public bool IsRequired { get; set; }
    }
}
