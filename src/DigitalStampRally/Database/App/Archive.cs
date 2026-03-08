using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class Archive
{
    public uint ArchiveId { get; set; }

    public string ArchiveUuid { get; set; } = null!;

    public uint WriterId { get; set; }

    public string BodyReference { get; set; } = null!;

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public uint Status { get; set; }

    public bool? InputCompleted { get; set; }

    public uint? NumberOfViews { get; set; }

    public uint? SalesPrice { get; set; }

    public string? ThumbnailPath { get; set; }

    public DateTime? CreateAt { get; set; }

    public DateTime? UpdateAt { get; set; }

    public DateTime? PublishAt { get; set; }

    public int? PriceSettingsId { get; set; }

    public bool? DeleteFlag { get; set; }

    public virtual ICollection<ArchiveHistory> ArchiveHistories { get; set; } = new List<ArchiveHistory>();

    public virtual ICollection<ArchiveReadLog> ArchiveReadLogs { get; set; } = new List<ArchiveReadLog>();

    public virtual PriceSetting? PriceSettings { get; set; }

    public virtual ICollection<UserArchiveBoughtLog> UserArchiveBoughtLogs { get; set; } = new List<UserArchiveBoughtLog>();

    public virtual Writer Writer { get; set; } = null!;

    public virtual ICollection<Request> RequestRequests { get; set; } = new List<Request>();

    public virtual ICollection<TagMgr> Tags { get; set; } = new List<TagMgr>();
}
