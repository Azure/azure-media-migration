using BitFaster.Caching.Lru;

namespace PlaybackService;

public class KeyCache
{
    private readonly ConcurrentLru<string, string> _cache = new ConcurrentLru<string, string>(capacity: 100000);

    public string? GetKey(string keyId)
    {
        return _cache.TryGet(keyId, out var key) ? key : null;
    }

    public void AddKey(string keyId, string key)
    {
        _cache.AddOrUpdate(keyId, key);
    }
}
