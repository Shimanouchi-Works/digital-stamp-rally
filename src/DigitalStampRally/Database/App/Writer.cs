using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class Writer
{
    public uint WriterId { get; set; }

    public uint UserId { get; set; }

    public string WriterName { get; set; } = null!;

    public string WriterNameEn { get; set; } = null!;

    public uint MeisterId { get; set; }

    public string? SelfIntroduction { get; set; }

    public string? IconFilePath { get; set; }

    public string? Link { get; set; }

    public uint WriterUnique { get; set; }

    public uint WriterEnUnique { get; set; }

    public DateTime? CreateAt { get; set; }

    public DateTime? UpdateAt { get; set; }

    public bool? DeleteFlag { get; set; }

    public virtual ICollection<Archive> Archives { get; set; } = new List<Archive>();

    public virtual Meister Meister { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
