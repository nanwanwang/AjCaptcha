using System.Collections.Concurrent;
using AjCaptcha.Core.Abstractions;
using AjCaptcha.Core.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AjCaptcha.Core.Infrastructure;

public sealed class MemoryCaptchaCacheStore : ICaptchaCacheStore
{
    private sealed class CacheEntry
    {
        public required string Value { get; init; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    }

    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly int _maxEntries;
    private readonly ILogger<MemoryCaptchaCacheStore> _logger;
    private readonly Timer? _cleanupTimer;

    public MemoryCaptchaCacheStore(AjCaptchaOptions options, ILogger<MemoryCaptchaCacheStore> logger)
    {
        _maxEntries = Math.Max(100, options.CacheNumber);
        _logger = logger;
        if (options.TimingClear > 0)
        {
            _cleanupTimer = new Timer(_ => CleanupExpired(), null, TimeSpan.FromSeconds(options.TimingClear), TimeSpan.FromSeconds(options.TimingClear));
        }
    }

    public Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        _entries[key] = new CacheEntry
        {
            Value = value,
            ExpiresAt = ttl.HasValue && ttl.Value > TimeSpan.Zero ? DateTimeOffset.UtcNow.Add(ttl.Value) : null
        };
        TrimIfNeeded();
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(TryGetEntry(key, out _));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(TryGetEntry(key, out var entry) ? entry.Value : null);

    public Task<long> IncrementAsync(string key, long value, CancellationToken cancellationToken = default)
    {
        var updated = _entries.AddOrUpdate(
            key,
            static (_, increment) => new CacheEntry { Value = increment.ToString(), ExpiresAt = null },
            static (_, existing, increment) =>
            {
                var current = long.TryParse(existing.Value, out var parsed) ? parsed : 0L;
                return new CacheEntry
                {
                    Value = (current + increment).ToString(),
                    ExpiresAt = existing.ExpiresAt
                };
            },
            value);

        return Task.FromResult(long.Parse(updated.Value));
    }

    public Task SetExpireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            entry.ExpiresAt = DateTimeOffset.UtcNow.Add(ttl);
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _cleanupTimer?.Dispose();
        _entries.Clear();
        return ValueTask.CompletedTask;
    }

    private bool TryGetEntry(string key, out CacheEntry entry)
    {
        if (_entries.TryGetValue(key, out entry!))
        {
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            {
                _entries.TryRemove(key, out _);
                return false;
            }

            return true;
        }

        return false;
    }

    private void CleanupExpired()
    {
        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAt.HasValue && pair.Value.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            {
                _entries.TryRemove(pair.Key, out _);
            }
        }
    }

    private void TrimIfNeeded()
    {
        if (_entries.Count <= _maxEntries)
        {
            return;
        }

        CleanupExpired();
        if (_entries.Count <= _maxEntries)
        {
            return;
        }

        var extra = _entries.Count - _maxEntries;
        foreach (var pair in _entries.OrderBy(x => x.Value.CreatedAt).Take(extra))
        {
            _entries.TryRemove(pair.Key, out _);
        }

        _logger.LogDebug("Trimmed in-memory captcha cache to {Count} entries.", _entries.Count);
    }
}

public sealed class RedisCaptchaCacheStore : ICaptchaCacheStore
{
    private readonly Lazy<Task<ConnectionMultiplexer>> _connectionFactory;
    private readonly string _instanceName;

    public RedisCaptchaCacheStore(AjCaptchaOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RedisConnectionString))
        {
            throw new InvalidOperationException("RedisConnectionString is required when CacheType is Redis.");
        }

        _instanceName = options.RedisInstanceName ?? string.Empty;
        _connectionFactory = new Lazy<Task<ConnectionMultiplexer>>(() => ConnectionMultiplexer.ConnectAsync(options.RedisConnectionString));
    }

    public async Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);
        await db.StringSetAsync(ToRedisKey(key), value, ttl).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);
        return await db.KeyExistsAsync(ToRedisKey(key)).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);
        await db.KeyDeleteAsync(ToRedisKey(key)).ConfigureAwait(false);
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var value = await db.StringGetAsync(ToRedisKey(key)).ConfigureAwait(false);
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task<long> IncrementAsync(string key, long value, CancellationToken cancellationToken = default)
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);
        return await db.StringIncrementAsync(ToRedisKey(key), value).ConfigureAwait(false);
    }

    public async Task SetExpireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);
        await db.KeyExpireAsync(ToRedisKey(key), ttl).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connectionFactory.IsValueCreated)
        {
            var connection = await _connectionFactory.Value.ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<IDatabase> GetDatabaseAsync()
    {
        var connection = await _connectionFactory.Value.ConfigureAwait(false);
        return connection.GetDatabase();
    }

    private RedisKey ToRedisKey(string key) => string.Concat(_instanceName, key);
}
