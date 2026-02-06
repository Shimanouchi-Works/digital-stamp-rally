using Microsoft.EntityFrameworkCore;
using DigitalStampRally.Database;

namespace DigitalStampRally.Services;

public class DbStampService
{
    private readonly DigitalStampRallyContext _db;
    public DbStampService(DigitalStampRallyContext db) => _db = db;

    public async Task<ParticipantSession> GetOrCreateSessionAsync(long eventId, string sessionKey, string userAgent, string ipHash)
    {
        var s = await _db.ParticipantSessions
            .FirstOrDefaultAsync(x => x.EventsId == eventId && x.SessionKey == sessionKey);

        if (s != null)
        {
            s.LastSeenAt = DateTime.Now;
            s.UserAgent = userAgent;
            await _db.SaveChangesAsync();
            return s;
        }

        s = new ParticipantSession
        {
            Id = IdUtil.NewId(),
            EventsId = eventId,
            SessionKey = sessionKey,
            FirstSeenAt = DateTime.Now,
            LastSeenAt = DateTime.Now,
            UserAgent = userAgent,
            IpHash = ipHash,
            IsBlocked = false
        };

        _db.ParticipantSessions.Add(s);
        await _db.SaveChangesAsync();
        return s;
    }

    // public Task<bool> IsGoaledAsync(long eventId, long sessionId)
    //     => _db.Goals.AnyAsync(g => g.EventsId == eventId && g.ParticipantSessionsId == sessionId);

    public async Task<string?> GetGoalCodeAsync(long eventId, long sessionId)
        => (await _db.Goals.FirstOrDefaultAsync(g => g.EventsId == eventId && g.ParticipantSessionsId == sessionId))?.AchievementCode;

    public async Task<string> EnsureGoalAsync(long eventId, long sessionId)
    {
        var existing = await _db.Goals.FirstOrDefaultAsync(g => g.EventsId == eventId && g.ParticipantSessionsId == sessionId);
        if (existing != null) return existing.AchievementCode;

        var code = Random.Shared.Next(0, 100_000_000).ToString("D8");

        _db.Goals.Add(new Goal
        {
            Id = IdUtil.NewId(),
            EventsId = eventId,
            ParticipantSessionsId = sessionId,
            GoaledAt = DateTime.Now,
            AchievementCode = code,
            CreatedAt = DateTime.Now
        });

        await _db.SaveChangesAsync();
        return code;
    }

    public async Task<(bool Stamped, int Result)> TryStampAsync(long eventId, long spotId, long sessionId, DateTime at, string rawToken)
    {
        var tokenHash = CryptoUtil.Sha256Hex(rawToken);

        // 既にゴール済みならブロック（result=5などにしてもOK。ここでは 5:ゴール済み）
        if (await IsGoaledAsync(eventId, sessionId))
        {
            _db.StampScanLogs.Add(new StampScanLog
            {
                Id = IdUtil.NewId(),
                EventsId = eventId,
                EventSpotsId = spotId,
                EventSpotsEventsId = eventId,
                ParticipantSessionsId = sessionId,
                ParticipantSessionsEventsId = eventId,
                ScannedAt = at,
                RawTokenHash = tokenHash,
                Result = 5,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            return (false, 5);
        }

        var exists = await _db.Stamps.AnyAsync(s =>
            s.EventsId == eventId &&
            s.EventSpotsId == spotId &&
            s.ParticipantSessionsId == sessionId);

        _db.StampScanLogs.Add(new StampScanLog
        {
            Id = IdUtil.NewId(),
            EventsId = eventId,
            EventSpotsId = spotId,
            EventSpotsEventsId = eventId,
            ParticipantSessionsId = sessionId,
            ParticipantSessionsEventsId = eventId,
            ScannedAt = at,
            RawTokenHash = tokenHash,
            Result = exists ? 1 : 0,
            CreatedAt = DateTime.Now
        });

        if (exists)
        {
            await _db.SaveChangesAsync();
            return (false, 1);
        }

        _db.Stamps.Add(new Stamp
        {
            Id = IdUtil.NewId(),
            EventsId = eventId,
            EventSpotsId = spotId,
            EventSpotsEventsId = eventId,
            ParticipantSessionsId = sessionId,
            ParticipantSessionsEventsId = eventId,
            StampedAt = at
        });

        await _db.SaveChangesAsync();
        return (true, 0);
    }

    public Task<int> CountStampedAsync(long eventId, long sessionId)
        => _db.Stamps.CountAsync(s => s.EventsId == eventId && s.ParticipantSessionsId == sessionId);

    public async Task<HashSet<long>> GetStampedSpotIdsAsync(long eventId, long sessionId)
    {
        var list = await _db.Stamps
            .Where(s => s.EventsId == eventId && s.ParticipantSessionsId == sessionId)
            .Select(s => s.EventSpotsId)
            .Distinct()
            .ToListAsync();

        return list.ToHashSet();
    }


    // 「達成コード」を作成/取得（goals に行を作るが goaled_at は NULL のまま）
    public async Task<string> EnsureAchievementCodeAsync(long eventId, long sessionId)
    {
        var existing = await _db.Goals
            .FirstOrDefaultAsync(g => g.EventsId == eventId && g.ParticipantSessionsId == sessionId);

        if (existing != null)
            return existing.AchievementCode;

        var code = Random.Shared.Next(0, 100_000_000).ToString("D8");

        _db.Goals.Add(new Goal
        {
            Id = IdUtil.NewId(),
            EventsId = eventId,
            ParticipantSessionsId = sessionId,
            GoaledAt = null, // ここがポイント（ゴール確定は SetGoal 側で）
            AchievementCode = code,
            CreatedAt = DateTime.Now
        });

        await _db.SaveChangesAsync();
        return code;
    }

    // 「ゴール済み」判定：goals が存在し、かつ goaled_at が埋まっている
    public Task<bool> IsGoaledAsync(long eventId, long sessionId)
        => _db.Goals.AnyAsync(g =>
            g.EventsId == eventId &&
            g.ParticipantSessionsId == sessionId &&
            g.GoaledAt != null);

    // ゴール確定：達成コードが無ければ作り、goaled_at を埋める
    public async Task<string> EnsureGoaledAsync(long eventId, long sessionId)
    {
        var g = await _db.Goals
            .FirstOrDefaultAsync(x => x.EventsId == eventId && x.ParticipantSessionsId == sessionId);

        if (g == null)
        {
            var code = Random.Shared.Next(0, 100_000_000).ToString("D8");
            g = new Goal
            {
                Id = IdUtil.NewId(),
                EventsId = eventId,
                ParticipantSessionsId = sessionId,
                AchievementCode = code,
                CreatedAt = DateTime.Now
            };
            _db.Goals.Add(g);
        }

        g.GoaledAt ??= DateTime.Now;
        await _db.SaveChangesAsync();
        return g.AchievementCode;
    }
}

