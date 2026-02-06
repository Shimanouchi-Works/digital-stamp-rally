namespace DigitalStampRally.Services;

public interface IStampLogStore
{
    Task AppendStampLogAsync(StampLog log);
}

public class StampLog
{
    public DateTime At { get; set; }
    public string EventId { get; set; } = "";
    public string SpotId { get; set; } = "";
    public string VisitorId { get; set; } = "";
    public bool IsDuplicate { get; set; }
    public string UserAgent { get; set; } = "";
}
