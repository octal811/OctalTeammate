using Microsoft.Extensions.Caching.Memory;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Services;

public class ApiAvailabilityService : IApiAvailabilityService
{
    private const string CachePrefix = "api-availability:";

    private readonly IApiAvailabilityRepository _repository;
    private readonly IMemoryCache _cache;

    public ApiAvailabilityService(IApiAvailabilityRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        var cacheKey = BuildCacheKey(key);

        if (_cache.TryGetValue(cacheKey, out bool cached))
            return cached;

        var entity = await _repository.FirstOrDefaultAsync(
            a => a.Key == key,
            cancellationToken);

        // Unknown keys default to enabled so unregistered features keep working,
        // and a row is only created once an administrator opts to control it.
        var enabled = entity?.IsEnabled ?? true;

        _cache.Set(cacheKey, enabled, TimeSpan.FromMinutes(5));

        return enabled;
    }

    public async Task EnableAsync(string key, Guid? updatedBy, CancellationToken cancellationToken = default)
    {
        await SetEnabledAsync(key, true, updatedBy, cancellationToken);
    }

    public async Task DisableAsync(string key, Guid? updatedBy, CancellationToken cancellationToken = default)
    {
        await SetEnabledAsync(key, false, updatedBy, cancellationToken);
    }

    public async Task<ApiAvailabilityStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        var entity = await _repository.FirstOrDefaultAsync(a => a.Key == key, cancellationToken);

        if (entity is null)
            return new ApiAvailabilityStatus(key, true, DateTime.UtcNow, null);

        return new ApiAvailabilityStatus(entity.Key, entity.IsEnabled, entity.UpdatedAt, entity.UpdatedBy);
    }

    private async Task SetEnabledAsync(
        string key,
        bool isEnabled,
        Guid? updatedBy,
        CancellationToken cancellationToken)
    {
        ValidateKey(key);

        var entity = await _repository.FirstOrDefaultAsync(a => a.Key == key, cancellationToken);

        if (entity is null)
        {
            entity = new ApiAvailability
            {
                Id = Guid.NewGuid(),
                Key = key,
                IsEnabled = isEnabled,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy,
                CreatedDate = DateTime.UtcNow
            };

            await _repository.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity.IsEnabled = isEnabled;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = updatedBy;
            entity.ModifiedDate = DateTime.UtcNow;

            _repository.Update(entity);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        // Invalidate the cached value so the change takes effect immediately.
        _cache.Remove(BuildCacheKey(key));
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("An availability key is required.", nameof(key));
    }

    private static string BuildCacheKey(string key)
        => CachePrefix + key;
}