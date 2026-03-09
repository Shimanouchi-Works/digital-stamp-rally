using System;
using System.Collections.Generic;

namespace DigitalStampRally.Database;

/// <summary>
/// QRが貼られている地点＝1レコード
/// </summary>
public partial class EventSpot
{
    public long Id { get; set; }

    public long EventsId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// 表示順
    /// </summary>
    public int? SortOrder { get; set; }

    public bool? IsActive { get; set; }

    public string? QrTokenHash { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Event Events { get; set; } = null!;

    public virtual ICollection<StampScanLog> StampScanLogs { get; set; } = new List<StampScanLog>();

    public virtual ICollection<Stamp> Stamps { get; set; } = new List<Stamp>();

    public virtual ICollection<EventReward> EventRewards { get; set; } = new List<EventReward>();
}
