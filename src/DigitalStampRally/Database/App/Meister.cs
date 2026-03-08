using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class Meister
{
    public uint MeisterId { get; set; }

    public string MeisterTitle { get; set; } = null!;

    public string? Description { get; set; }

    public uint? RegistereUserId { get; set; }

    public int? Status { get; set; }

    public virtual ICollection<Writer> Writers { get; set; } = new List<Writer>();
}
