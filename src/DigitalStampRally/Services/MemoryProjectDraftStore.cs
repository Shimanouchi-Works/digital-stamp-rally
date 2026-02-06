using Microsoft.Extensions.Caching.Memory;

namespace DigitalStampRally.Services;

public class MemoryProjectDraftStore : IProjectDraftStore
{
    private readonly IMemoryCache _cache;

    public MemoryProjectDraftStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Save(string json)
    {
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()); // 短くて衝突しにくい
        _cache.Set(Key(token), json, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) // 30分だけ保持
        });
        return token;
    }

    public bool TryGet(string token, out string json)
    {
        return _cache.TryGetValue(Key(token), out json!);
    }

    public void Remove(string token)
    {
        _cache.Remove(Key(token));
    }

    private static string Key(string token) => $"draft:{token}";
}
