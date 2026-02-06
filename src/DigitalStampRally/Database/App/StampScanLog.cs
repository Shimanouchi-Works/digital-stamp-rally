using System;
using System.Collections.Generic;

namespace DigitalStampRally.Database;

/// <summary>
/// 「読み取った」事実を残す。押印済みでもログは残してOK。
/// </summary>
public partial class StampScanLog
{
    public long Id { get; set; }

    public long EventsId { get; set; }

    public long EventSpotsId { get; set; }

    public long EventSpotsEventsId { get; set; }

    public long ParticipantSessionsId { get; set; }

    public long ParticipantSessionsEventsId { get; set; }

    /// <summary>
    /// 読み取り時刻
    /// </summary>
    public DateTime? ScannedAt { get; set; }

    /// <summary>
    /// 0:成功 1:重複 2:無効QR 3:停止中 4:期限外…
    /// </summary>
    public int? Result { get; set; }

    public string? RawTokenHash { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual EventSpot EventSpot { get; set; } = null!;

    public virtual Event Events { get; set; } = null!;

    public virtual ParticipantSession ParticipantSession { get; set; } = null!;
}
