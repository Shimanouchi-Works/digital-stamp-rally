using Microsoft.Extensions.Caching.Memory;
using DigitalStampRally.Models;

namespace DigitalStampRally.Services;

public class MemoryProjectDraftStore : IProjectDraftStore
{
    private readonly IMemoryCache _cache;

    public MemoryProjectDraftStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    // ----------------------------
    // 保存（JSON + 画像）
    // ----------------------------
    public string Save(string json, DraftImagePayload? image = null)
    {
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray());

        var payload = new ProjectDraftPayload
        {
            Json = json,
            EventImage = image
        };

        _cache.Set(Key(token), payload, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
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

    private static string Key(string token) => $"draft:{token}";
}
