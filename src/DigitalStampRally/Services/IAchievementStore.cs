namespace DigitalStampRally.Services;

public interface IAchievementStore
{
    Task<string> GetOrCreateAsync(string eventId, string visitorId);
}
