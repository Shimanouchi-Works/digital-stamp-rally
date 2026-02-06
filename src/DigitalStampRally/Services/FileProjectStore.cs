using System.Text.Json;
using DigitalStampRally.Models;

namespace DigitalStampRally.Services;

public class FileProjectStore : IProjectStore
{
    private readonly string _rootDir;
    private readonly JsonSerializerOptions _json;

    public FileProjectStore(IWebHostEnvironment env)
    {
        _rootDir = Path.Combine(env.ContentRootPath, "App_Data", "projects");
        Directory.CreateDirectory(_rootDir);

        _json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public bool TryGet(string eventId, out ProjectDto project)
    {
        project = default!;
        var path = Path.Combine(_rootDir, $"{eventId}.json");
        if (!File.Exists(path)) return false;

        try
        {
            var json = File.ReadAllText(path);
            var p = JsonSerializer.Deserialize<ProjectDto>(json, _json);
            if (p == null) return false;
            project = p;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task SaveAsync(ProjectDto project)
    {
        var path = Path.Combine(_rootDir, $"{project.EventId}.json");
        var json = JsonSerializer.Serialize(project, _json);
        await File.WriteAllTextAsync(path, json);
    }
}
