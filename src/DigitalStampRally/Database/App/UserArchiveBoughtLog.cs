using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class UserArchiveBoughtLog
{
    public long Id { get; set; }

    public uint UserId { get; set; }

    public uint ArchiveId { get; set; }

    public DateTime? BaughtDate { get; set; }

    public uint? Price { get; set; }

    /// <summary>
    /// ライターの収益
    /// </summary>
    public int? WriterRevenue { get; set; }

    public int? Status { get; set; }

    public string? StripeCheckoutSessionId { get; set; }

    public string? StripePaymentIntentId { get; set; }

    public DateTime? PaymentConfirmedAt { get; set; }

    public virtual Archive Archive { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
