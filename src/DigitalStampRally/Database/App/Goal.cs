using System;
using System.Collections.Generic;

namespace DigitalStampRally.Database;

public partial class Goal
{
    public long Id { get; set; }

    public long EventsId { get; set; }

    public long ParticipantSessionsId { get; set; }

    public DateTime? GoaledAt { get; set; }

    public string AchievementCode { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual Event Events { get; set; } = null!;

    public virtual ParticipantSession ParticipantSession { get; set; } = null!;
}
