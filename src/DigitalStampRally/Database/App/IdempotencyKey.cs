using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class IdempotencyKey
{
    public string Key { get; set; } = null!;

    public string? Handler { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Processing=0/Completed=1
    /// </summary>
    public int Status { get; set; }
}
