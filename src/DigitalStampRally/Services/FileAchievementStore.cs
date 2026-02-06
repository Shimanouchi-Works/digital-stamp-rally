using System.Text.Json;

namespace DigitalStampRally.Services;

public class FileAchievementStore : IAchievementStore
{
    private readonly string _rootDir;

    public FileAchievementStore(IWebHostEnvironment env)
    {
        _rootDir = Path.Combine(env.ContentRootPath, "App_Data", "achievements");
        Directory.CreateDirectory(_rootDir);
    }

    public async Task<string> GetOrCreateAsync(string eventId, string visitorId)
    {
        var path = Path.Combine(_rootDir, $"{eventId}.json");

        Dictionary<string, string> map;
        if (File.Exists(path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path);
                map = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
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

        if (map.TryGetValue(visitorId, out var existing))
            return existing;

        // 8桁 10進数（先頭ゼロあり）
        var code = Random.Shared.Next(0, 100_000_000).ToString("D8");

        // 衝突回避（同一event内で重複があれば引き直し）
        var used = map.Values.ToHashSet();
        for (int i = 0; i < 10 && used.Contains(code); i++)
            code = Random.Shared.Next(0, 100_000_000).ToString("D8");

        map[visitorId] = code;

        var outJson = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, outJson);

        return code;
    }
}
