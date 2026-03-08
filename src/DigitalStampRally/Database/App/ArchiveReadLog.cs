using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class ArchiveReadLog
{
    public int Id { get; set; }

    public uint ArchiveId { get; set; }

    public uint? UserId { get; set; }

    public bool? BaughtFlag { get; set; }

    public DateTime? ReadDate { get; set; }

    public virtual Archive Archive { get; set; } = null!;

    public virtual User? User { get; set; }
}
