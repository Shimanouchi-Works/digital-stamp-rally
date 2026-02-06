using System.Text.Json;

namespace DigitalStampRally.Services;

public class FileGoalStore : IGoalStore
{
    private readonly string _rootDir;

    public FileGoalStore(IWebHostEnvironment env)
    {
        _rootDir = Path.Combine(env.ContentRootPath, "App_Data", "goals");
        Directory.CreateDirectory(_rootDir);
    }

    public async Task<GoalRecord?> GetAsync(string eventId, string visitorId)
    {
        var path = Path.Combine(_rootDir, $"{eventId}.json");
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var map = JsonSerializer.Deserialize<Dictionary<string, GoalRecord>>(json) ?? new();
            return map.TryGetValue(visitorId, out var r) ? r : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetAsync(GoalRecord record)
    {
        var path = Path.Combine(_rootDir, $"{record.EventId}.json");

        Dictionary<string, GoalRecord> map;
        if (File.Exists(path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path);
                map = JsonSerializer.Deserialize<Dictionary<string, GoalRecord>>(json) ?? new();
            }
            catch
            {
                map = new();
            }
        }
        else
        {
            map = new();
        }

        map[record.VisitorId] = record;

        var outJson = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, outJson);
    }
}
