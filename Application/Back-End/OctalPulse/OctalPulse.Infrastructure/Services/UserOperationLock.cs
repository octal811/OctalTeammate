using System.Collections.Concurrent;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Infrastructure.Services;

public sealed class UserOperationLock : IUserOperationLock
{
    private static readonly TimeSpan EntryIdleTime = TimeSpan.FromMinutes(5);
    private const int MaxEntries = 10_000;

    private readonly ConcurrentDictionary<LockKey, LockEntry> _entries = new();

    public IDisposable? TryAcquire(Guid userId, string operation)
    {
        var key = new LockKey(userId, operation);

        while (true)
        {
            var entry = _entries.GetOrAdd(key, static _ => new LockEntry());

            lock (entry)
            {
                if (entry.IsActive)
                    return null;

                entry.IsActive = true;
                entry.LastUsedUtc = DateTime.UtcNow;
            }

            if (_entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                return new LockLease(this, key, entry);

            lock (entry)
            {
                entry.IsActive = false;
            }
        }
    }

    private void Release(LockKey key, LockEntry entry)
    {
        lock (entry)
        {
            entry.IsActive = false;
            entry.LastUsedUtc = DateTime.UtcNow;
        }

        if (_entries.Count > MaxEntries)
            EvictIdleEntries();
    }

    private void EvictIdleEntries()
    {
        var cutoff = DateTime.UtcNow - EntryIdleTime;

        foreach (var kvp in _entries)
        {
            lock (kvp.Value)
            {
                if (!kvp.Value.IsActive && kvp.Value.LastUsedUtc < cutoff)
                    _entries.TryRemove(kvp);
            }
        }
    }

    private sealed class LockLease : IDisposable
    {
        private readonly UserOperationLock _owner;
        private readonly LockKey _key;
        private LockEntry? _entry;

        public LockLease(UserOperationLock owner, LockKey key, LockEntry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        public void Dispose()
        {
            var entry = Interlocked.Exchange(ref _entry, null);
            if (entry is not null)
                _owner.Release(_key, entry);
        }
    }

    private sealed class LockEntry
    {
        public bool IsActive;
        public DateTime LastUsedUtc = DateTime.UtcNow;
    }

    private readonly record struct LockKey(Guid UserId, string Operation);
}