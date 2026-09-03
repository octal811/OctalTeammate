using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Infrastructure.Services;

public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new();

    public CacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        var options = new MemoryCacheEntryOptions();
        if (expiry.HasValue)
            options.SetAbsoluteExpiration(expiry.Value);

        SetCore(key, value, options);
    }

    public void Set<T>(string key, T value, DateTimeOffset absoluteExpiration)
    {
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(absoluteExpiration);

        SetCore(key, value, options);
    }

    public bool TryGet<T>(string key, out T? value)
    {
        value = default;

        if (!_cache.TryGetValue(key, out var cached) || cached is null)
            return false;

        value = (T)cached;
        return true;
    }

    public T? Get<T>(string key)
    {
        TryGet(key, out T? value);
        return value;
    }

    public bool Exists(string key)
    {
        return _cache.TryGetValue(key, out _);
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
    }

    public void RemoveByPrefix(string prefix)
    {
        foreach (var key in _keys.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                Remove(key);
        }
    }

    private void SetCore<T>(string key, T value, MemoryCacheEntryOptions options)
    {
        options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string k)
                _keys.TryRemove(k, out _);
        });

        _keys.TryAdd(key, 0);
        _cache.Set(key, value, options);
    }
}