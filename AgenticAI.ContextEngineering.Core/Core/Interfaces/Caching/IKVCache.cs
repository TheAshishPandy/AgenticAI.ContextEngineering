using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    /// <summary>
    /// Key-Value Cache Interface
    /// </summary>
    public interface IKVCache
    {
        /// <summary>
        /// Get value by key
        /// </summary>
        Task<T> GetAsync<T>(string key) where T : class;

        /// <summary>
        /// Set value with key
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Get or set value atomically
        /// </summary>
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Check if key exists
        /// </summary>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Remove key
        /// </summary>
        Task RemoveAsync(string key);

        /// <summary>
        /// Remove by pattern
        /// </summary>
        Task RemoveByPatternAsync(string pattern);

        /// <summary>
        /// Clear all cache
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Get all keys
        /// </summary>
        Task<List<string>> GetKeysAsync();

        /// <summary>
        /// Get cache statistics
        /// </summary>
        CacheStatistics GetStatistics();

        /// <summary>
        /// Cache name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Item count
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Size in bytes
        /// </summary>
        long Size { get; }
    }

    /// <summary>
    /// Cache Statistics
    /// </summary>
    public class CacheStatistics
    {
        public string CacheName { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public long TotalSizeBytes { get; set; }
        public long Hits { get; set; }
        public long Misses { get; set; }
        public long Evictions { get; set; }
        public long TotalRequests => Hits + Misses;
        public double HitRate => TotalRequests > 0 ? (double)Hits / TotalRequests : 0;
        public TimeSpan AverageResponseTime { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool IsConnected { get; set; } = true;
        public string ConnectionStatus { get; set; } = "Connected";
    }

    /// <summary>
    /// Cache Report
    /// </summary>
    public class CacheReport
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string ApplicationName { get; set; } = string.Empty;
        public Dictionary<string, CacheStatistics> Tiers { get; set; } = new();
        public CacheStatistics Total { get; set; } = new();
        public List<CacheHitRateHistory> HitRateHistory { get; set; } = new();
        public List<CacheSizeHistory> SizeHistory { get; set; } = new();
        public Dictionary<string, object> Recommendations { get; set; } = new();
        public DistributedCacheHealth DistributedCacheHealth { get; set; } = new();

        public double OverallHitRate => Total.HitRate;
        public long TotalSize => Total.TotalSizeBytes;
        public int TotalItems => Total.TotalItems;
    }

    public class DistributedCacheHealth
    {
        public bool IsConnected { get; set; }
        public string Status { get; set; } = "Unknown";
        public double LatencyMs { get; set; }
        public DateTime LastCheck { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class CacheHitRateHistory
    {
        public DateTime Timestamp { get; set; }
        public double HitRate { get; set; }
        public string Tier { get; set; } = string.Empty;
    }

    public class CacheSizeHistory
    {
        public DateTime Timestamp { get; set; }
        public long SizeBytes { get; set; }
        public int ItemCount { get; set; }
        public string Tier { get; set; } = string.Empty;
    }
}