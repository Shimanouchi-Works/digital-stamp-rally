namespace DigitalStampRally.Services;

public interface IGoalStore
{
    Task<GoalRecord?> GetAsync(string eventId, string visitorId);
    Task SetAsync(GoalRecord record);
}

public class GoalRecord
{
    public string EventId { get; set; } = "";
    public string VisitorId { get; set; } = "";
    public DateTime GoaledAt { get; set; }
    public string AchievementCode { get; set; } = "";
    public List<string> CollectedSpotIds { get; set; } = new();
}
