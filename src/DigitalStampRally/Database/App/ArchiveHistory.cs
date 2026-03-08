using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class ArchiveHistory
{
    public uint ArchiveId { get; set; }

    public uint HistoryVersion { get; set; }

    public DateTime UpdateAt { get; set; }

    public uint UpdateType { get; set; }

    public string? BodyReference { get; set; }

    public string? UpdateDescription { get; set; }

    public virtual Archive Archive { get; set; } = null!;
}
