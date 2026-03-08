using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class Request
{
    public uint RequestId { get; set; }

    public string RequestUuid { get; set; } = null!;

    /// <summary>
    /// 希望の専門
    /// </summary>
    public uint? DesiredMeisterId { get; set; }

    public uint? UserId { get; set; }

    public string? RequestTitle { get; set; }

    public string? RequestBody { get; set; }

    public bool? DeleteFlag { get; set; }

    public DateTime? CreateAt { get; set; }

    public DateTime? UpdateAt { get; set; }

    public virtual User? User { get; set; }

    public virtual ICollection<Archive> ArchivesArchives { get; set; } = new List<Archive>();

    public virtual ICollection<TagMgr> Tags { get; set; } = new List<TagMgr>();
}
