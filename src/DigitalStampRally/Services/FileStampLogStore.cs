using System.Text.Json;

namespace DigitalStampRally.Services;

public class FileStampLogStore : IStampLogStore
{
    private readonly string _rootDir;

    public FileStampLogStore(IWebHostEnvironment env)
    {
        _rootDir = Path.Combine(env.ContentRootPath, "App_Data", "logs");
        Directory.CreateDirectory(_rootDir);
    }

    public async Task AppendStampLogAsync(StampLog log)
    {
        var path = Path.Combine(_rootDir, $"{log.EventId}_stamp.log");
        var line = JsonSerializer.Serialize(log);
        await File.AppendAllTextAsync(path, line + Environment.NewLine);
    }
}
