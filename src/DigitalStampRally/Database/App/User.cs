using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class User
{
    public uint UserId { get; set; }

    public string? IdentityUserId { get; set; }

    public DateTime? Birth { get; set; }

    public uint? Gender { get; set; }

    public string? SosialLoginId { get; set; }

    public string? ExternalPaymentId { get; set; }

    public string? ExternalCustmerId { get; set; }

    public DateTime? CreateAt { get; set; }

    public DateTime? UpdateAt { get; set; }

    public bool? DeleteFlag { get; set; }

    public virtual ICollection<ArchiveReadLog> ArchiveReadLogs { get; set; } = new List<ArchiveReadLog>();

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual ICollection<UserArchiveBoughtLog> UserArchiveBoughtLogs { get; set; } = new List<UserArchiveBoughtLog>();

    public virtual ICollection<Writer> Writers { get; set; } = new List<Writer>();
}
