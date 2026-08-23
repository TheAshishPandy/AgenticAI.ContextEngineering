using AgenticAI.ContextEngineering.Core.Core.Models.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    public class MemoryKVCache : IKVCache
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryKVCache> _logger;
        private readonly ConcurrentDictionary<string, CacheItemMetadata> _metadata = new();
        private readonly object _statsLock = new object();
        private CacheStatistics _statistics = new() { CacheName = "Memory" };

        public string Name => "Memory";
        public int Count => _metadata.Count;
        public long Size => _metadata.Values.Sum(m => m.SizeEstimate);

        public MemoryKVCache(IMemoryCache cache, ILogger<MemoryKVCache> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            try
            {
                if (_cache.TryGetValue(key, out T value))
                {
                    lock (_statsLock)
                    {
                        _statistics.Hits++;
                        if (_metadata.TryGetValue(key, out var meta))
                        {
                            meta.AccessCount++;
                            meta.LastAccessed = DateTime.UtcNow;
                        }
                    }
                    return await Task.FromResult(value);
                }

                lock (_statsLock) _statistics.Misses++;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"MemoryCache Get error: {key}");
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                if (value == null) return;

                var ttl = expiration ?? TimeSpan.FromHours(2);
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                    SlidingExpiration = TimeSpan.FromMinutes(30),
                    Priority = CacheItemPriority.High
                };

                options.RegisterPostEvictionCallback((k, v, reason, state) =>
                {
                    lock (_statsLock) _statistics.Evictions++;
                    _metadata.TryRemove(k.ToString(), out _);
                });

                _cache.Set(key, value, options);

                var size = JsonSerializer.Serialize(value).Length;
                _metadata[key] = new CacheItemMetadata
                {
                    Created = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow,
                    AccessCount = 1,
                    SizeEstimate = size,
                    Expiry = DateTime.UtcNow.Add(ttl)
                };

                lock (_statsLock)
                {
                    _statistics.TotalItems = _metadata.Count;
                    _statistics.TotalSizeBytes = Size;
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"MemoryCache Set error: {key}");
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var value = await GetAsync<T>(key);
            if (value != null) return value;

            value = await factory();
            if (value != null) await SetAsync(key, value, expiration);

            return value;
        }

        public async Task<bool> ExistsAsync(string key)
            => await Task.FromResult(_cache.TryGetValue(key, out _));

        public async Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            _metadata.TryRemove(key, out _);
            lock (_statsLock)
            {
                _statistics.TotalItems = _metadata.Count;
                _statistics.TotalSizeBytes = Size;
            }
            await Task.CompletedTask;
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            var keys = _metadata.Keys.Where(k =>
                k.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in keys) await RemoveAsync(key);
        }

        public async Task ClearAsync()
        {
            if (_cache is MemoryCache memoryCache) memoryCache.Dispose();
            _metadata.Clear();
            lock (_statsLock)
            {
                _statistics.TotalItems = 0;
                _statistics.TotalSizeBytes = 0;
            }
            await Task.CompletedTask;
        }

        public async Task<List<string>> GetKeysAsync()
            => await Task.FromResult(_metadata.Keys.ToList());

        public CacheStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                _statistics.TotalItems = _metadata.Count;
                _statistics.TotalSizeBytes = Size;
                _statistics.LastUpdated = DateTime.UtcNow;
                return _statistics;
            }
        }

      
    }
}