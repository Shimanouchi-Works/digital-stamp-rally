using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DigitalStampRally.Database;
using DigitalStampRally.Services;

namespace DigitalStampRally.Pages;

[IgnoreAntiforgeryToken]
public class TotalizeModel : PageModel
{
    private readonly DigitalStampRallyContext _db;
    private readonly DbEventService _eventService;

    public TotalizeModel(DigitalStampRallyContext db, DbEventService eventService)
    {
        _db = db;
        _eventService = eventService;
    }

    // 入力（クエリ）
    public long EventId { get; private set; }
    public string Token { get; private set; } = "";

    // 表示
    public bool IsAuthorized { get; private set; }
    public string EventTitle { get; private set; } = "";
    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }

    public string? ErrorMessage { get; private set; }
    public string? AuthErrorMessage { get; private set; }

    public List<SpotView> Spots { get; private set; } = new();

    // 集計結果
    public int GoalTotal { get; private set; }
    public int TotalStampReads { get; private set; }

    public Dictionary<long, int> TotalBySpot { get; private set; } = new();
    public Dictionary<long, List<HourCountRow>> HourlyBySpot { get; private set; } = new();
    public List<HourCountRow> GoalsByHour { get; private set; } = new();

    // GET: パスワード入力画面
    public async Task<IActionResult> OnGetAsync(long? e, string? t)
    {
        if (e == null || string.IsNullOrWhiteSpace(t))
        {
            ErrorMessage = "URLの情報が不足しています。";
            return Page();
        }

        EventId = e.Value;
        Token = t;

        var ev = await _eventService.GetEventAsync(EventId);
        if (ev == null)
        {
            ErrorMessage = "イベントが見つかりませんでした。";
            return Page();
        }

        // トークン検証（hash）
        if (!await _eventService.ValidateTotalizeTokenAsync(EventId, Token))
        {
            ErrorMessage = "集計画面トークンが無効です。";
            return Page();
        }

        EventTitle = ev.Title;
        ValidFrom = ev.StartsAt;
        ValidTo = ev.EndsAt;

        return Page();
    }

    // POST: パスワード認証して集計表示
    public async Task<IActionResult> OnPostAuthAsync(long? e, string? t, string? password)
    {
        if (e == null || string.IsNullOrWhiteSpace(t))
        {
            ErrorMessage = "URLの情報が不足しています。";
            return Page();
        }

        EventId = e.Value;
        Token = t;

        var ev = await _eventService.GetEventAsync(EventId);
        if (ev == null)
        {
            ErrorMessage = "イベントが見つかりませんでした。";
            return Page();
        }

        if (!await _eventService.ValidateTotalizeTokenAsync(EventId, Token))
        {
            ErrorMessage = "集計画面トークンが無効です。";
            return Page();
        }

        EventTitle = ev.Title;
        ValidFrom = ev.StartsAt;
        ValidTo = ev.EndsAt;

        // パスワード検証（hash）
        if (string.IsNullOrWhiteSpace(password) || !await _eventService.ValidateTotalizePasswordAsync(EventId, password))
        {
            AuthErrorMessage = "パスワードが違います。";
            return Page();
        }

        IsAuthorized = true;

        // スポット一覧
        var spots = await _db.EventSpots
            .Where(x => x.EventsId == EventId && (x.IsActive == null || x.IsActive == true))
            .OrderBy(x => x.SortOrder ?? 0)
            .ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();

        // 必須スポット（event_rewards(type=1/2, active)に紐づくスポット）
        var requiredSpotIds = await GetRequiredSpotIdsAsync(EventId);

        Spots = spots
            .Select(s => new SpotView
            {
                SpotId = s.Id,
                SpotName = s.Name,
                IsRequired = requiredSpotIds.Contains(s.Id)
            })
            .ToList();

        // --------------------
        // 集計（DB）
        // --------------------

        // 押印相当：stamps は一意（重複除外済み）
        var stamps = _db.Stamps
            .Where(x => x.EventsId == EventId && x.StampedAt != null);

        TotalStampReads = await stamps.CountAsync();

        // 合計（spot別）
        TotalBySpot = await stamps
            .GroupBy(x => x.EventSpotsId)
            .Select(g => new { SpotId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SpotId, x => x.Count);

        // 毎時（spot別）
        var stampHourRows = await stamps
            .GroupBy(x => new { x.EventSpotsId, Hour = TruncToHour(x.StampedAt!.Value) })
            .Select(g => new { g.Key.EventSpotsId, g.Key.Hour, Count = g.Count() })
            .OrderBy(x => x.EventSpotsId)
            .ThenBy(x => x.Hour)
            .ToListAsync();

        HourlyBySpot = stampHourRows
            .GroupBy(x => x.EventSpotsId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new HourCountRow { Hour = x.Hour, Count = x.Count }).ToList()
            );

        // ゴール人数（合計・毎時）
        var goals = _db.Goals
            .Where(x => x.EventsId == EventId && x.GoaledAt != null);

        GoalTotal = await goals.CountAsync();

        var goalHourRows = await goals
            .GroupBy(x => TruncToHour(x.GoaledAt!.Value))
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .OrderBy(x => x.Hour)
            .ToListAsync();

        GoalsByHour = goalHourRows
            .Select(x => new HourCountRow { Hour = x.Hour, Count = x.Count })
            .ToList();

        return Page();
    }

    private async Task<HashSet<long>> GetRequiredSpotIdsAsync(long eventId)
    {
        var rows = await _db.Set<RequiredSpotRow>()
            .FromSqlInterpolated($@"
                SELECT DISTINCT rrs.event_spots_id
                FROM reward_required_spots rrs
                JOIN event_rewards er
                ON er.id = rrs.event_rewards_id
                AND er.events_id = rrs.event_rewards_events_id
                WHERE er.events_id = {eventId}
                AND (er.is_active IS NULL OR er.is_active = 1)
                AND er.type IN (1,2)
            ")
            .ToListAsync();

        return rows.Select(x => x.EventSpotsId).ToHashSet();
    }


    private static DateTime TruncToHour(DateTime dt)
        => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0);

    // --------------------
    // 表示用モデル
    // --------------------
    public class SpotView
    {
        public long SpotId { get; set; }
        public string SpotName { get; set; } = "";
        public bool IsRequired { get; set; }
    }

    public class HourCountRow
    {
        public DateTime Hour { get; set; }
        public int Count { get; set; }
        public string HourLabel => Hour.ToString("yyyy/MM/dd HH:00");
    }
}
