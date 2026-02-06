using System;
using System.Collections.Generic;

namespace DigitalStampRally.Database;

/// <summary>
/// 景品/達成条件
/// </summary>
public partial class EventReward
{
    public long Id { get; set; }

    public long EventsId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// 0:スタンプ数達成 1:必須スポット 2:両方
    /// </summary>
    public int? Type { get; set; }

    /// <summary>
    /// typeに応じて
    /// </summary>
    public int? RequiredStampCount { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Event Events { get; set; } = null!;

    public virtual ICollection<EventSpot> EventSpots { get; set; } = new List<EventSpot>();
}
