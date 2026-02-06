using System;
using System.Collections.Generic;

namespace DigitalStampRally.Database;

/// <summary>
/// 「この参加者はこのスポットのスタンプを持っている」を一意にするテーブル。
/// </summary>
public partial class Stamp
{
    public long Id { get; set; }

    public long EventsId { get; set; }

    public long EventSpotsId { get; set; }

    public long EventSpotsEventsId { get; set; }

    public long ParticipantSessionsId { get; set; }

    public long ParticipantSessionsEventsId { get; set; }

    /// <summary>
    /// 初回押印時刻
    /// </summary>
    public DateTime? StampedAt { get; set; }

    public virtual EventSpot EventSpot { get; set; } = null!;

    public virtual Event Events { get; set; } = null!;

    public virtual ParticipantSession ParticipantSession { get; set; } = null!;
}
