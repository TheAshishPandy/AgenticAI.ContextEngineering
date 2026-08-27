// AgenticAI.ContextEngineering.Core/Caching/MultiTierKVCache.cs
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    /// <summary>
    /// Simple Multi-Tier Cache - Simplified version
    /// </summary>
    public class MultiTierKVCache : IKVCache
    {
        private readonly IKVCache _l1Cache;  // Memory
        private readonly IKVCache? _l2Cache; // File
        private readonly IKVCache? _l3Cache; // Distributed
        private readonly ILogger<MultiTierKVCache> _logger;

        public string Name => "MultiTier";
        public int Count => GetTotalCount();
        public long Size => GetTotalSize();

        public MultiTierKVCache(
            IKVCache l1Cache,
            IKVCache? l2Cache,
            IKVCache? l3Cache,
            ILogger<MultiTierKVCache> logger)
        {
            _l1Cache = l1Cache ?? throw new ArgumentNullException(nameof(l1Cache));
            _l2Cache = l2Cache;
            _l3Cache = l3Cache;
            _logger = logger;

            _logger.LogInformation($"🗄️ MultiTierCache initialized with L1:{l1Cache.Name}, L2:{l2Cache?.Name ?? "None"}, L3:{l3Cache?.Name ?? "None"}");
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            // Try L1 first
            var result = await _l1Cache.GetAsync<T>(key);
            if (result != null)
            {
                _logger.LogDebug($"✅ Cache HIT (L1): {key}");
                return result;
            }

            // Try L2
            if (_l2Cache != null)
            {
                result = await _l2Cache.GetAsync<T>(key);
                if (result != null)
                {
                    _logger.LogDebug($"✅ Cache HIT (L2): {key}");
                    // Promote to L1
                    await _l1Cache.SetAsync(key, result);
                    return result;
                }
            }

            // Try L3
            if (_l3Cache != null)
            {
                result = await _l3Cache.GetAsync<T>(key);
                if (result != null)
                {
                    _logger.LogDebug($"✅ Cache HIT (L3): {key}");
                    // Promote to L1
                    await _l1Cache.SetAsync(key, result);
                    if (_l2Cache != null)
                    {
                        await _l2Cache.SetAsync(key, result);
                    }
                    return result;
                }
            }

            _logger.LogDebug($"❌ Cache MISS: {key}");
            return null;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            // Set in all tiers
            await _l1Cache.SetAsync(key, value, expiration);

            if (_l2Cache != null)
                await _l2Cache.SetAsync(key, value, expiration);

            if (_l3Cache != null)
                await _l3Cache.SetAsync(key, value, expiration);

            _logger.LogDebug($"✅ Set cache: {key}");
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var cached = await GetAsync<T>(key);
            if (cached != null)
                return cached;

            var result = await factory();
            if (result != null)
            {
                await SetAsync(key, result, expiration);
            }

            return result;
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (await _l1Cache.ExistsAsync(key))
                return true;

            if (_l2Cache != null && await _l2Cache.ExistsAsync(key))
                return true;

            if (_l3Cache != null && await _l3Cache.ExistsAsync(key))
                return true;

            return false;
        }

        public async Task RemoveAsync(string key)
        {
            await _l1Cache.RemoveAsync(key);

            if (_l2Cache != null)
                await _l2Cache.RemoveAsync(key);

            if (_l3Cache != null)
                await _l3Cache.RemoveAsync(key);
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            await _l1Cache.RemoveByPatternAsync(pattern);

            if (_l2Cache != null)
                await _l2Cache.RemoveByPatternAsync(pattern);

            if (_l3Cache != null)
                await _l3Cache.RemoveByPatternAsync(pattern);
        }

        public async Task ClearAsync()
        {
            await _l1Cache.ClearAsync();

            if (_l2Cache != null)
                await _l2Cache.ClearAsync();

            if (_l3Cache != null)
                await _l3Cache.ClearAsync();
        }

        public async Task<List<string>> GetKeysAsync()
        {
            var allKeys = new HashSet<string>();

            var keys1 = await _l1Cache.GetKeysAsync();
            foreach (var key in keys1)
                allKeys.Add(key);

            if (_l2Cache != null)
            {
                var keys2 = await _l2Cache.GetKeysAsync();
                foreach (var key in keys2)
                    allKeys.Add(key);
            }

            if (_l3Cache != null)
            {
                var keys3 = await _l3Cache.GetKeysAsync();
                foreach (var key in keys3)
                    allKeys.Add(key);
            }

            return allKeys.ToList();
        }

        public CacheStatistics GetStatistics()
        {
            var totalStats = new CacheStatistics
            {
                CacheName = "MultiTier",
                LastUpdated = DateTime.UtcNow
            };

            var stats1 = _l1Cache.GetStatistics();
            totalStats.TotalItems += stats1.TotalItems;
            totalStats.TotalSizeBytes += stats1.TotalSizeBytes;
            totalStats.Hits += stats1.Hits;
            totalStats.Misses += stats1.Misses;

            if (_l2Cache != null)
            {
                var stats2 = _l2Cache.GetStatistics();
                totalStats.TotalItems += stats2.TotalItems;
                totalStats.TotalSizeBytes += stats2.TotalSizeBytes;
                totalStats.Hits += stats2.Hits;
                totalStats.Misses += stats2.Misses;
            }

            if (_l3Cache != null)
            {
                var stats3 = _l3Cache.GetStatistics();
                totalStats.TotalItems += stats3.TotalItems;
                totalStats.TotalSizeBytes += stats3.TotalSizeBytes;
                totalStats.Hits += stats3.Hits;
                totalStats.Misses += stats3.Misses;
            }

            return totalStats;
        }

        public Dictionary<string, CacheStatistics> GetAllTierStatistics()
        {
            var result = new Dictionary<string, CacheStatistics>();

            var stats1 = _l1Cache.GetStatistics();
            stats1.CacheName = "Memory";
            result["Memory"] = stats1;

            if (_l2Cache != null)
            {
                var stats2 = _l2Cache.GetStatistics();
                stats2.CacheName = "File";
                result["File"] = stats2;
            }

            if (_l3Cache != null)
            {
                var stats3 = _l3Cache.GetStatistics();
                stats3.CacheName = "Distributed";
                result["Distributed"] = stats3;
            }

            return result;
        }

        private int GetTotalCount()
        {
            var count = _l1Cache.Count;
            if (_l2Cache != null) count += _l2Cache.Count;
            if (_l3Cache != null) count += _l3Cache.Count;
            return count;
        }

        private long GetTotalSize()
        {
            var size = _l1Cache.Size;
            if (_l2Cache != null) size += _l2Cache.Size;
            if (_l3Cache != null) size += _l3Cache.Size;
            return size;
        }
    }
}