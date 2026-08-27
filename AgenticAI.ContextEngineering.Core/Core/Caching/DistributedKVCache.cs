// AgenticAI.ContextEngineering.Core/Caching/DistributedKVCache.cs
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    public class DistributedKVCache : IKVCache
    {
        private readonly string _connectionString;
        private readonly ILogger<DistributedKVCache>? _logger;
        private readonly Dictionary<string, (object Value, DateTime Expiry)> _cache = new();

        public string Name => "Distributed";
        public int Count => _cache.Count;
        public long Size => 0;

        public DistributedKVCache(string connectionString, ILogger<DistributedKVCache>? logger = null)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public Task<T> GetAsync<T>(string key) where T : class
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.Expiry > DateTime.UtcNow)
                {
                    return Task.FromResult(entry.Value as T);
                }
                _cache.Remove(key);
            }
            return Task.FromResult<T>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            var expiry = expiration.HasValue
                ? DateTime.UtcNow.Add(expiration.Value)
                : DateTime.UtcNow.AddDays(1);
            _cache[key] = (value, expiry);
            return Task.CompletedTask;
        }

        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var result = GetAsync<T>(key).Result;
            if (result != null)
                return Task.FromResult(result);

            var value = factory().Result;
            SetAsync(key, value, expiration).Wait();
            return Task.FromResult(value);
        }

        public Task<bool> ExistsAsync(string key)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.Expiry > DateTime.UtcNow)
                    return Task.FromResult(true);
                _cache.Remove(key);
            }
            return Task.FromResult(false);
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPatternAsync(string pattern)
        {
            var keysToRemove = new List<string>();
            foreach (var key in _cache.Keys)
            {
                if (key.Contains(pattern))
                    keysToRemove.Add(key);
            }
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            _cache.Clear();
            return Task.CompletedTask;
        }

        public Task<List<string>> GetKeysAsync()
        {
            return Task.FromResult(new List<string>(_cache.Keys));
        }

        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                CacheName = "Distributed",
                TotalItems = _cache.Count,
                LastUpdated = DateTime.UtcNow
            };
        }
    }
}