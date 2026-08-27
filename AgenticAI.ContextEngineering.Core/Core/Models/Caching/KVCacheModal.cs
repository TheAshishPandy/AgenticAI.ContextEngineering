using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Core.Models.Caching
{

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
    public class CacheItemMetadata
    {
        public DateTime Created { get; set; }
        public DateTime LastAccessed { get; set; }
        public long AccessCount { get; set; }
        public long SizeEstimate { get; set; }
        public DateTime Expiry { get; set; }
    }
    public class KVCacheOptions
    {
        // Memory Cache
        public bool EnableMemoryCache { get; set; } = true;
        public TimeSpan MemoryCacheExpiration { get; set; } = TimeSpan.FromHours(2);

        // File Cache
        public bool EnableFileCache { get; set; } = true;
        public string FileCacheDirectory { get; set; } = "Cache/KV";
        public TimeSpan FileCacheExpiration { get; set; } = TimeSpan.FromDays(7);

        // Distributed Cache
        public bool EnableDistributedCache { get; set; } = false;
        public string DistributedCacheProvider { get; set; } = "Redis"; // "Redis" or "SqlServer"
        public string RedisConnectionString { get; set; } = "localhost:6379";
        public string RedisInstanceName { get; set; } = "SmartChatBot_";
        public string SqlConnectionString { get; set; } = string.Empty;
        public string SqlSchemaName { get; set; } = "dbo";
        public string SqlTableName { get; set; } = "Cache";
        public TimeSpan DistributedCacheExpiration { get; set; } = TimeSpan.FromDays(7);
        public int DefaultExpirationMinutes { get; set; } = 60;  
        public int DefaultExpirationHours { get; set; } = 60;  
        public int MaxSizeMB { get; set; } = 100;  

        // Features
        public bool EnableCompression { get; set; } = true;
        public bool EnableMetrics { get; set; } = true;
        public bool EnableCacheWarming { get; set; } = true;
    }
}

