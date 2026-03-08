using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class PendingRegistration
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string? IdentityUserId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime ExpireAt { get; set; }
}
