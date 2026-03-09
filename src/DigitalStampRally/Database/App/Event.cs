using System;
using System.Collections.Generic;

namespace DigitalStampRally.Database;

public partial class Event
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>
    /// 0:下書き 1:公開 2:終了 3:停止
    /// </summary>
    public int Status { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public int? RequiredStampCount { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? GoalTokenHash { get; set; }

    public string? TotalizeTokenHash { get; set; }

    public string? TotalizePasswordHash { get; set; }

    public virtual ICollection<EventReward> EventRewards { get; set; } = new List<EventReward>();

    public virtual ICollection<EventSpot> EventSpots { get; set; } = new List<EventSpot>();

    public virtual ICollection<Goal> Goals { get; set; } = new List<Goal>();

    public virtual ICollection<ParticipantSession> ParticipantSessions { get; set; } = new List<ParticipantSession>();

    public virtual ICollection<StampScanLog> StampScanLogs { get; set; } = new List<StampScanLog>();

    public virtual ICollection<Stamp> Stamps { get; set; } = new List<Stamp>();
}
