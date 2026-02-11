using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using DigitalStampRally.Models;

namespace DigitalStampRally.Services;

public class MemoryProjectDraftStore : IProjectDraftStore
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;

    public MemoryProjectDraftStore(IMemoryCache cache, IConfiguration config)
    {
        _cache = cache;
        _config = config;
    }

    // ----------------------------
    // 保存（JSON + 画像）
    // ----------------------------
    public string Save(string json, DraftImagePayload? image = null)
    {
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray());

        var absoluteMinutes = GetInt("DraftStore:AbsoluteExpirationMinutes", 120);
        var slidingMinutes  = GetInt("DraftStore:SlidingExpirationMinutes", 30);

        var payload = new ProjectDraftPayload
        {
            Json = json,
            EventImage = image
        };

        _cache.Set(Key(token), payload, new MemoryCacheEntryOptions
        {
            // 最大保持時間（強制上限）
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(absoluteMinutes),
            // これだけアクセスが無いと削除
            SlidingExpiration = TimeSpan.FromMinutes(slidingMinutes)
        });

        return token;
    }

    // ----------------------------
    // 取得
    // ----------------------------
    public bool TryGet(string token, out ProjectDraftPayload payload)
    {
        return _cache.TryGetValue(Key(token), out payload!);
    }

    // ----------------------------
    // 削除
    // ----------------------------
    public void Remove(string token)
    {
        _cache.Remove(Key(token));
    }

    private int GetInt(string key, int defaultValue)
    {
        var str = _config[key];
        return int.TryParse(str, out var value) ? value : defaultValue;
    }

    private static string Key(string token) => $"draft:{token}";
}
