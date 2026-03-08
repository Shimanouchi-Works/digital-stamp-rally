using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class StripeWebhookEvent
{
    public string StripeEventId { get; set; } = null!;

    public DateTime ProcessedAt { get; set; }
}
