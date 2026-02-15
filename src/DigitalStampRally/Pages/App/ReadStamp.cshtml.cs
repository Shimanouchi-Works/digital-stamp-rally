using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DigitalStampRally.Services;

namespace DigitalStampRally.Pages;

[IgnoreAntiforgeryToken]
public class ReadStampModel : PageModel
{
    private readonly DbEventService _eventService;
    private readonly DbStampService _stampService;

    public ReadStampModel(DbEventService eventService, DbStampService stampService)
    {
        _eventService = eventService;
        _stampService = stampService;
    }

    // 画面表示用（DBはBIGINTなので long）
    public long EventId { get; private set; }
    public long SpotId { get; private set; }
    public string Token { get; private set; } = ""; // JSがStamp APIを叩く時に必要
    public string SpotName { get; private set; } = "";
    public string EventTitle { get; private set; } = "";
    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }

    public List<long> RequiredSpotIds { get; private set; } = new();
    public Dictionary<long, SpotMeta> SpotMap { get; private set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(long? e, long? s, string? t)
    {
        ViewData["NoIndex"] = true; // ロボット防止

        try
        {
            if (e == null || s == null || string.IsNullOrWhiteSpace(t))
            {
                ErrorMessage = "エラーが発生しました(001)。";//"QRコードの情報が不足しています。";
                return Page();
            }

            EventId = e.Value;
            SpotId = s.Value;
            Token = t;

            var ev = await _eventService.GetEventAsync(EventId);
            if (ev == null)
            {
                ErrorMessage =  "エラーが発生しました(002)。";//"このイベントは見つかりませんでした。";
                return Page();
            }

            // 期限チェック
            var now = DateTime.Now;
            if (ev.StartsAt != null && now < ev.StartsAt)
            {
                ErrorMessage =  "このQRコードは有効期限外です（開始前）。";
                return Page();
            }
            if (ev.EndsAt != null && now > ev.EndsAt)
            {
                ErrorMessage =  "このQRコードは有効期限外です（終了後）。";
                return Page();
                }

            // スポット＆トークン検証（hashで照合）
            var valid = await _eventService.ValidateSpotTokenAsync(EventId, SpotId, Token);
            if (!valid)
            {
                ErrorMessage =  "エラーが発生しました(003)。";//"QRコードが無効です。";
                return Page();
            }

            var spots = await _eventService.GetActiveSpotsAsync(EventId);
            var spot = spots.FirstOrDefault(x => x.Id == SpotId);
            if (spot == null)
            {
                ErrorMessage =  "エラーが発生しました(004)。";//"掲示場所が見つかりませんでした。";
                return Page();
            }

            EventTitle = ev.Title;
            ValidFrom = ev.StartsAt;
            ValidTo = ev.EndsAt;
            SpotName = spot.Name;

            var required = await _eventService.GetRequiredSpotIdsAsync(EventId);
            RequiredSpotIds = required.ToList();

            SpotMap = spots.ToDictionary(
                x => x.Id,
                x => new SpotMeta { Name = x.Name, IsRequired = required.Contains(x.Id) }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorMessage = "不明なエラー";
        }

        return Page();
    }

    // ---- Ajax: 押印（DBで確定）----
#if DEBUG
    public async Task<IActionResult> OnPostStampAsync([FromBody] StampRequest req, string? date)
#else
    public async Task<IActionResult> OnPostStampAsync([FromBody] StampRequest req)
#endif
    {
        try
        {
            if (req == null || req.EventId <= 0 || req.SpotId <= 0 || string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.VisitorId))
                return new JsonResult(new {
                        success = false,
                        message =  "エラーが発生しました(011)。",//"リクエスト不正"
                        });

#if DEBUG
#else
            // token再検証（直叩き対策）
            if (!await _eventService.ValidateSpotTokenAsync(req.EventId, req.SpotId, req.Token))
                return new JsonResult(new { 
                        success = false,
                        message = "エラーが発生しました(012)。",//"無効なQRです"
                        });
#endif

            var ua = Request.Headers.UserAgent.ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var ipHash = CryptoUtil.Sha256Hex(ip);

Console.WriteLine($"OnPostStampAsync: visitor={req.VisitorId} spot={req.SpotId}");
            var session = await _stampService.GetOrCreateSessionAsync(req.EventId, req.VisitorId, ua, ipHash);

            if (session.IsBlocked ?? false)
                return new JsonResult(new { 
                            success = false,
                            message = "エラーが発生しました(013)。",//"ブロックされています"
                            });

            // ゴール済みは追加押印を拒否（仕様）
            if (await _stampService.IsGoaledAsync(req.EventId, session.Id))
                return new JsonResult(new { success = true, result = 5, stamped = false });

            DateTime stampTime;
            #if DEBUG
            stampTime = !string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsed) ? parsed : DateTime.Now;
            #else
            stampTime = DateTime.Now;
            #endif
            var (stamped, result) = await _stampService.TryStampAsync(
                            req.EventId,
                            req.SpotId,
                            session.Id,
                            stampTime,
                            req.Token);
            // result: 0成功 1重複 5ゴール済み（DbStampService側の実装に合わせる）

            return new JsonResult(new { success = true, stamped, result });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorMessage = "不明なエラー";
            return new JsonResult(new { success = false});
        }
    }

    // ---- Ajax: 達成コード（DBで判定・保存）----
    public async Task<IActionResult> OnPostAchievementAsync([FromBody] AchievementRequest req)
    {
        try
        {
            if (req == null || req.EventId <= 0 || string.IsNullOrWhiteSpace(req.VisitorId))
                return new JsonResult(new { 
                            success = false,
                            message = "エラーが発生しました(021)。",//"リクエスト不正"
                            });

            var ev = await _eventService.GetEventAsync(req.EventId);
            if (ev == null)
                return new JsonResult(new { 
                            success = false,
                            message = "エラーが発生しました(022)。",//"イベント不明"
                            });

            // 期限チェック
            var now = DateTime.Now;
            if (ev.StartsAt != null && now < ev.StartsAt) return new JsonResult(new { success = false, message = "期限外（開始前）" });
            if (ev.EndsAt != null && now > ev.EndsAt) return new JsonResult(new { success = false, message = "期限外（終了後）" });

            var ua = Request.Headers.UserAgent.ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var ipHash = CryptoUtil.Sha256Hex(ip);

            var session = await _stampService.GetOrCreateSessionAsync(req.EventId, req.VisitorId, ua, ipHash);

            // 必須達成チェック：DB stamps で判定（クライアント申告は信用しない）
            var required = await _eventService.GetRequiredSpotIdsAsync(req.EventId);
            if (required.Count > 0)
            {
                var stamped = await _stampService.GetStampedSpotIdsAsync(req.EventId, session.Id);
                if (!required.All(stamped.Contains))
                    return new JsonResult(new { success = false, message = "まだ条件を満たしていません" });
            }

            // 達成コードを保存/取得（goals を台帳にする）
            var code = await _stampService.EnsureAchievementCodeAsync(req.EventId, session.Id);

            return new JsonResult(new { success = true, code });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorMessage = "不明なエラー";
            return new JsonResult(new { success = false });
        }
    }

    // ---- Ajax: ゴール済み確認（goals.goaled_at の有無）----
    public async Task<IActionResult> OnPostGoalStatusAsync([FromBody] GoalStatusRequest req)
    {
        try
        {
            if (req == null || req.EventId <= 0 || string.IsNullOrWhiteSpace(req.VisitorId))
                return new JsonResult(new { success = false });

            var ua = Request.Headers.UserAgent.ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var ipHash = CryptoUtil.Sha256Hex(ip);

            var session = await _stampService.GetOrCreateSessionAsync(req.EventId, req.VisitorId, ua, ipHash);

            var goaled = await _stampService.IsGoaledAsync(req.EventId, session.Id);
            return new JsonResult(new { success = true, goaled });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ErrorMessage = "不明なエラー";
            return new JsonResult(new { success = false });
        }
    }

    // ---- request models ----
    public class StampRequest
    {
        public long EventId { get; set; }
        public long SpotId { get; set; }
        public string Token { get; set; } = "";
        public string VisitorId { get; set; } = "";
    }

    public class AchievementRequest
    {
        public long EventId { get; set; }
        public string VisitorId { get; set; } = "";
    }

    public class SpotMeta
    {
        public string Name { get; set; } = "";
        public bool IsRequired { get; set; }
    }

    public class GoalStatusRequest
    {
        public long EventId { get; set; }
        public string VisitorId { get; set; } = "";
    }
}
