// AgenticAI.ContextEngineering.Core/Caching/MultiTierKVCache.cs
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    /// <summary>
    /// Multi-tier cache implementation that uses Memory, File, and Distributed caches
    /// </summary>
    public class MultiTierKVCache : IKVCache
    {
        private readonly MemoryKVCache _memoryCache;
        private readonly FileKVCache _fileCache;
        private readonly DistributedKVCache _distributedCache;
        private readonly ILogger<MultiTierKVCache> _logger;

        public string Name => "MultiTier";
        public int Count => _memoryCache.Count + _fileCache.Count + _distributedCache.Count;
        public long Size => _memoryCache.Size + _fileCache.Size + _distributedCache.Size;

        public MultiTierKVCache(
            MemoryKVCache memoryCache,
            FileKVCache fileCache,
            DistributedKVCache distributedCache,
            ILogger<MultiTierKVCache> logger = null)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _fileCache = fileCache ?? throw new ArgumentNullException(nameof(fileCache));
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _logger = logger;
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            // Try memory first (fastest)
            var result = await _memoryCache.GetAsync<T>(key);
            if (result != null)
            {
                _logger?.LogDebug("Cache hit in Memory tier for key: {Key}", key);
                return result;
            }

            // Try file cache (medium)
            result = await _fileCache.GetAsync<T>(key);
            if (result != null)
            {
                _logger?.LogDebug("Cache hit in File tier for key: {Key}", key);
                // Promote to memory
                await _memoryCache.SetAsync(key, result);
                return result;
            }

            // Try distributed cache (slowest)
            result = await _distributedCache.GetAsync<T>(key);
            if (result != null)
            {
                _logger?.LogDebug("Cache hit in Distributed tier for key: {Key}", key);
                // Promote to memory and file
                await _memoryCache.SetAsync(key, result);
                await _fileCache.SetAsync(key, result);
                return result;
            }

            _logger?.LogDebug("Cache miss for key: {Key}", key);
            return null;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            await _memoryCache.SetAsync(key, value, expiration);
            await _fileCache.SetAsync(key, value, expiration);
            await _distributedCache.SetAsync(key, value, expiration);
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var cached = await GetAsync<T>(key);
            if (cached != null)
            {
                return cached;
            }

            var value = await factory();
            if (value != null)
            {
                await SetAsync(key, value, expiration);
            }
            return value;
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await _memoryCache.ExistsAsync(key) ||
                   await _fileCache.ExistsAsync(key) ||
                   await _distributedCache.ExistsAsync(key);
        }

        public async Task RemoveAsync(string key)
        {
            await _memoryCache.RemoveAsync(key);
            await _fileCache.RemoveAsync(key);
            await _distributedCache.RemoveAsync(key);
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            await _memoryCache.RemoveByPatternAsync(pattern);
            await _fileCache.RemoveByPatternAsync(pattern);
            await _distributedCache.RemoveByPatternAsync(pattern);
        }

        public async Task ClearAsync()
        {
            await _memoryCache.ClearAsync();
            await _fileCache.ClearAsync();
            await _distributedCache.ClearAsync();
        }

        public async Task<List<string>> GetKeysAsync()
        {
            var memoryKeys = await _memoryCache.GetKeysAsync();
            var fileKeys = await _fileCache.GetKeysAsync();
            var distributedKeys = await _distributedCache.GetKeysAsync();

            return memoryKeys.Union(fileKeys).Union(distributedKeys).ToList();
        }

        public CacheStatistics GetStatistics()
        {
            var memoryStats = _memoryCache.GetStatistics();
            var fileStats = _fileCache.GetStatistics();
            var distributedStats = _distributedCache.GetStatistics();

            return new CacheStatistics
            {
                CacheName = "MultiTier",
                TotalItems = memoryStats.TotalItems + fileStats.TotalItems + distributedStats.TotalItems,
                TotalSizeBytes = memoryStats.TotalSizeBytes + fileStats.TotalSizeBytes + distributedStats.TotalSizeBytes,
                Hits = memoryStats.Hits + fileStats.Hits + distributedStats.Hits,
                Misses = memoryStats.Misses + fileStats.Misses + distributedStats.Misses,
                Evictions = memoryStats.Evictions + fileStats.Evictions + distributedStats.Evictions,
                LastUpdated = DateTime.UtcNow,
                IsConnected = memoryStats.IsConnected && fileStats.IsConnected && distributedStats.IsConnected,
                ConnectionStatus = GetConnectionStatus(memoryStats, fileStats, distributedStats),
                Metadata = new Dictionary<string, object>
                {
                    ["MemoryCache"] = memoryStats,
                    ["FileCache"] = fileStats,
                    ["DistributedCache"] = distributedStats
                }
            };
        }

        private string GetConnectionStatus(CacheStatistics memory, CacheStatistics file, CacheStatistics distributed)
        {
            if (!memory.IsConnected) return "Memory Disconnected";
            if (!file.IsConnected) return "File Disconnected";
            if (!distributed.IsConnected) return "Distributed Disconnected";
            return "Connected";
        }

        /// <summary>
        /// Get statistics for all tiers
        /// </summary>
        public Dictionary<string, CacheStatistics> GetAllTierStatistics()
        {
            return new Dictionary<string, CacheStatistics>
            {
                ["Memory"] = _memoryCache.GetStatistics(),
                ["File"] = _fileCache.GetStatistics(),
                ["Distributed"] = _distributedCache.GetStatistics()
            };
        }

        /// <summary>
        /// Generate cache report
        /// </summary>
        public async Task<CacheReport> GenerateReportAsync()
        {
            var report = new CacheReport
            {
                GeneratedAt = DateTime.UtcNow,
                ApplicationName = "AgenticAI.ContextEngineering"
            };

            var allStats = GetAllTierStatistics();
            foreach (var tier in allStats)
            {
                report.Tiers[tier.Key] = tier.Value;
            }

            report.Total = GetStatistics();
            report.DistributedCacheHealth = new DistributedCacheHealth
            {
                IsConnected = true,
                Status = "Healthy",
                LatencyMs = 0,
                LastCheck = DateTime.UtcNow
            };

            report.Recommendations = new Dictionary<string, object>
            {
                ["MemoryCacheSize"] = "Consider increasing memory cache size if hit rate is low",
                ["FileCachePath"] = "Ensure file cache directory has sufficient disk space",
                ["DistributedCache"] = "Monitor distributed cache connectivity"
            };

            return report;
        }
    }
}