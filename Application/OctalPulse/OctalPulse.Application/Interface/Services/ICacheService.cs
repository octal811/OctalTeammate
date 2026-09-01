namespace OctalPulse.Application.Interface.Services;

public interface ICacheService
{
    T? Get<T>(string key);
    bool TryGet<T>(string key, out T? value);
    bool Exists(string key);

    void Set<T>(string key, T value, TimeSpan? expiry = null);
    void Set<T>(string key, T value, DateTimeOffset absoluteExpiration);

    void Remove(string key);
    void RemoveByPrefix(string prefix);
}