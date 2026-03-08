using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class TagMgr
{
    public uint TagId { get; set; }

    public string TagString { get; set; } = null!;

    public virtual ICollection<Archive> Archives { get; set; } = new List<Archive>();

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();
}
