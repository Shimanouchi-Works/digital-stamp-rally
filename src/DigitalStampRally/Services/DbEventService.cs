using Microsoft.EntityFrameworkCore;
using DigitalStampRally.Database;

namespace DigitalStampRally.Services;

public class DbEventService
{
    private readonly DigitalStampRallyContext _db;
    public DbEventService(DigitalStampRallyContext db) => _db = db;

    public Task<Event?> GetEventAsync(long eventId)
        => _db.Events.FirstOrDefaultAsync(e => e.Id == eventId);

    public Task<List<EventSpot>> GetActiveSpotsAsync(long eventId)
        => _db.EventSpots
            .Where(s => s.EventsId == eventId && (s.IsActive ?? false))
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

    public async Task<HashSet<long>> GetRequiredSpotIdsAsync(long eventId)
    {
        // type=1:必須スポット
        var rewardIds = await _db.EventRewards
            .Where(r => r.EventsId == eventId && (r.IsActive ?? false) && r.Type == 1)
            .Select(r => r.Id)
            .ToListAsync();

        if (rewardIds.Count == 0) return new HashSet<long>();

        // scaffold の many-to-many は shadow entity "RewardRequiredSpot"
        var rows = await _db.Set<Dictionary<string, object>>("RewardRequiredSpot")
            .Where(x =>
                rewardIds.Contains((long)x["EventRewardsId"]) &&
                (long)x["EventRewardsEventsId"] == eventId)
            .Select(x => (long)x["EventSpotsId"])
            .ToListAsync();

        return rows.ToHashSet();
    }

    public async Task<bool> ValidateSpotTokenAsync(long eventId, long spotId, string rawToken)
    {
        var hash = CryptoUtil.Sha256Hex(rawToken);
        return await _db.EventSpots.AnyAsync(s =>
            s.EventsId == eventId &&
            s.Id == spotId &&
            s.QrTokenHash == hash);
    }

    public Task<bool> ValidateGoalTokenAsync(long eventId, string rawToken)
    {
        var hash = CryptoUtil.Sha256Hex(rawToken);
        return _db.Events.AnyAsync(e => e.Id == eventId && e.GoalTokenHash == hash);
    }

    public async Task<bool> ValidateTotalizeAsync(long eventId, string rawToken, string password)
    {
        var ev = await GetEventAsync(eventId);
        if (ev == null) return false;

        return ev.TotalizeTokenHash == CryptoUtil.Sha256Hex(rawToken)
            && ev.TotalizePasswordHash == CryptoUtil.Sha256Hex(password);
    }

    public async Task<CreateEventResult> CreateEventAsync(
        string title,
        DateTime startsAt,
        DateTime endsAt,
        List<(string Name, bool IsRequired)> spots)
    {
        //var eventId = IdUtil.NewId();

        var goalToken = CryptoUtil.NewToken();
        var totalizeToken = CryptoUtil.NewToken();
        var totalizePassword = CryptoUtil.NewPassword();

        var ev = new Event
        {
            //Id = eventId,
            Title = title,
            Status = 1,
            StartsAt = startsAt,
            EndsAt = endsAt,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            GoalTokenHash = CryptoUtil.Sha256Hex(goalToken),
            TotalizeTokenHash = CryptoUtil.Sha256Hex(totalizeToken),
            TotalizePasswordHash = CryptoUtil.Sha256Hex(totalizePassword)
        };

        var spotEntities = new List<(EventSpot Entity, string Token, bool IsRequired)>();
        int order = 1;

        foreach (var s in spots)
        {
            //var spotId = IdUtil.NewId();
            var spotToken = CryptoUtil.NewToken();

            var spot = new EventSpot
            {
                //Id = spotId,
                //EventsId = eventId,
                Name = s.Name,
                SortOrder = order++,
                IsActive = true,
                QrTokenHash = CryptoUtil.Sha256Hex(spotToken),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,

                Events = ev
            };

            spotEntities.Add((spot, spotToken, s.IsRequired));
            _db.EventSpots.Add(spot);

            //createdSpots.Add(new CreatedSpot(spotId, s.Name, spotToken, s.IsRequired));
        }

        // 必須スポットを reward_required_spots に入れる（event_rewards type=1 を1件作る）
        //var rewardId = IdUtil.NewId();
        var reward = new EventReward
        {
            //Id = rewardId,
            //EventsId = eventId,
            Title = "必須スポット達成",
            Type = 1,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,

            Events = ev
        };
        _db.EventRewards.Add(reward);

        // join（shadow entityを使うなら、Idではなく「参照で結べる形」にしたいが、
        // 今のDictionary方式はIdが必要になりがちなので、ここは SaveChanges後に入れるのが安全）
        _db.Events.Add(ev);

        await _db.SaveChangesAsync();

        // foreach (var s in createdSpots.Where(x => x.IsRequired))
        // {
        //     _db.Add(new Dictionary<string, object>
        //     {
        //         ["EventRewardsId"] = rewardId,
        //         ["EventRewardsEventsId"] = eventId,
        //         ["EventSpotsId"] = s.SpotId,
        //         ["EventSpotsEventsId"] = eventId
        //     });
        // }
        foreach (var x in spotEntities.Where(x => x.IsRequired))
        {
            _db.Set<Dictionary<string, object>>("RewardRequiredSpot").Add(new Dictionary<string, object>
            {
                ["EventRewardsId"] = reward.Id,
                ["EventRewardsEventsId"] = ev.Id,
                ["EventSpotsId"] = x.Entity.Id,          // ← ここは x.Entity.Id じゃなく DBのID（long）にするのが基本
                ["EventSpotsEventsId"] = ev.Id
            });
        }
        await _db.SaveChangesAsync();

        var createdSpots = spotEntities
            .Select(x => new CreatedSpot(x.Entity.Id, x.Entity.Name, x.Token, x.IsRequired))
            .ToList();

        return new CreateEventResult(ev.Id, goalToken, totalizeToken, totalizePassword, createdSpots);
    }

    public Task<bool> ValidateTotalizeTokenAsync(long eventId, string rawToken)
    {
        var hash = CryptoUtil.Sha256Hex(rawToken);
        return _db.Events.AnyAsync(e => e.Id == eventId && e.TotalizeTokenHash == hash);
    }

    public Task<bool> ValidateTotalizePasswordAsync(long eventId, string rawPassword)
    {
        var hash = CryptoUtil.Sha256Hex(rawPassword);
        return _db.Events.AnyAsync(e => e.Id == eventId && e.TotalizePasswordHash == hash);
    }

}

public record CreatedSpot(long SpotId, string Name, string SpotToken, bool IsRequired);

public record CreateEventResult(
    long EventId,
    string GoalToken,
    string TotalizeToken,
    string TotalizePassword,
    List<CreatedSpot> Spots);
