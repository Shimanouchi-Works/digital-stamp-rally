using System;
using System.Collections.Generic;

namespace DigitalStampRally.Database;

/// <summary>
/// ログインなし参加者の“台帳”
/// </summary>
public partial class ParticipantSession
{
    public long Id { get; set; }

    public long EventsId { get; set; }

    /// <summary>
    /// UUID等（端末に保存）
    /// </summary>
    public string? SessionKey { get; set; }

    /// <summary>
    /// 参加者が最初にこのイベントに来た時刻
    /// </summary>
    public DateTime? FirstSeenAt { get; set; }

    /// <summary>
    /// 最後に何か操作した時刻
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    public string? UserAgent { get; set; }

    public string? IpHash { get; set; }

    public bool? IsBlocked { get; set; }

    public virtual Event Events { get; set; } = null!;

    public virtual Goal? Goal { get; set; }

    public virtual ICollection<StampScanLog> StampScanLogs { get; set; } = new List<StampScanLog>();

    public virtual ICollection<Stamp> Stamps { get; set; } = new List<Stamp>();
}
