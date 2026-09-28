namespace Zadatak34TriviaServer_Projekat2.Services;

public enum CacheResultSource { Hit, Loaded, Waited }

public sealed record CacheResult(string Body, int StatusCode, CacheResultSource Source);

public sealed class TriviaCache
{
    private sealed record CacheEntry(string Body, int StatusCode, DateTime ExpiresAt);

    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly Dictionary<string, TaskCompletionSource<CacheEntry>> _inFlight = new();
    private readonly TimeSpan _expiration;
    private readonly Logger _logger;

    public TriviaCache(TimeSpan expiration, Logger logger)
    {
        _expiration = expiration;
        _logger = logger;
    }

    public async Task<CacheResult> GetOrCreateAsync(
        string key, Func<Task<(string Body, int StatusCode)>> factory)
    {
        TaskCompletionSource<CacheEntry>? source;
        bool owner = false;

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                if (cached.ExpiresAt > DateTime.UtcNow)
                {
                    _logger.Info($"CACHE HIT: {key}");
                    return new CacheResult(cached.Body, cached.StatusCode, CacheResultSource.Hit);
                }

                _cache.Remove(key);
                _logger.Info($"CACHE EXPIRED: {key}");
            }

            if (_inFlight.TryGetValue(key, out source))
            {
                _logger.Info($"CACHE WAIT: {key}");
            }
            else
            {
                source = new TaskCompletionSource<CacheEntry>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _inFlight[key] = source;
                owner = true;
                _logger.Info($"CACHE MISS: {key}; ovaj task učitava resurs.");
            }
        }

        if (!owner)
        {
            var entry = await source!.Task;
            return new CacheResult(entry.Body, entry.StatusCode, CacheResultSource.Waited);
        }

        try
        {
            // Mrežni poziv je VAN lock-a.
            var (body, status) = await factory();
            var entry = new CacheEntry(body, status, DateTime.UtcNow.Add(_expiration));

            lock (_cacheLock)
            {
                _cache[key] = entry;
                _inFlight.Remove(key);
            }

            source!.TrySetResult(entry);
            _logger.Info($"CACHE STORE: {key}; ističe {entry.ExpiresAt:HH:mm:ss}");
            return new CacheResult(body, status, CacheResultSource.Loaded);
        }
        catch (Exception ex)
        {
            lock (_cacheLock) _inFlight.Remove(key);
            source!.TrySetException(ex);
            _logger.Error($"CACHE LOAD ERROR: {key} - {ex.Message}");
            throw;
        }
    }
}
