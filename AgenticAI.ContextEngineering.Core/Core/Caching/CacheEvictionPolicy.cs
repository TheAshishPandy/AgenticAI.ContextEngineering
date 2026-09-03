// AgenticAI.ContextEngineering.Core/Core/Caching/CacheEvictionPolicy.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    /// <summary>
    /// Represents cache entry metadata for eviction decisions.
    /// </summary>
    public class CacheEntry
    {
        public string Key { get; set; }
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public int AccessCount { get; set; }
    }

    /// <summary>
    /// Eviction policy for managing cache size and retention.
    /// Implements LRU (Least Recently Used) with size and TTL constraints.
    /// </summary>
    public class CacheEvictionPolicy
    {
        /// <summary>
        /// Maximum total cache size in bytes (default: 500 MB).
        /// </summary>
        public long MaxTotalSizeBytes { get; }

        /// <summary>
        /// Maximum size per individual entry (default: 50 MB).
        /// </summary>
        public long MaxEntrySizeBytes { get; }

        /// <summary>
        /// Maximum time-to-live for any cache entry (default: 1 hour).
        /// </summary>
        public TimeSpan MaxTimeToLive { get; }

        /// <summary>
        /// Threshold percentage at which eviction starts (default: 80%).
        /// </summary>
        public int EvictionThresholdPercent { get; }

        /// <summary>
        /// Initializes eviction policy with default values compatible with most deployments.
        /// </summary>
        public CacheEvictionPolicy(
            long? maxTotalSizeBytes = null,
            long? maxEntrySizeBytes = null,
            TimeSpan? maxTimeToLive = null,
            int evictionThresholdPercent = 80)
        {
            MaxTotalSizeBytes = maxTotalSizeBytes ?? (500L * 1024 * 1024);  // 500 MB
            MaxEntrySizeBytes = maxEntrySizeBytes ?? (50L * 1024 * 1024);   // 50 MB
            MaxTimeToLive = maxTimeToLive ?? TimeSpan.FromHours(1);
            EvictionThresholdPercent = Math.Max(1, Math.Min(99, evictionThresholdPercent));
        }

        /// <summary>
        /// Determines if an entry is expired based on TTL policy.
        /// </summary>
        public bool IsExpired(CacheEntry entry)
        {
            if (entry == null)
                return true;

            var age = DateTime.UtcNow - entry.CreatedAt;
            return age > MaxTimeToLive;
        }

        /// <summary>
        /// Determines if cache is at or over eviction threshold.
        /// </summary>
        public bool ShouldEvict(long currentSizeBytes)
        {
            long thresholdBytes = (MaxTotalSizeBytes * EvictionThresholdPercent) / 100;
            return currentSizeBytes >= thresholdBytes;
        }

        /// <summary>
        /// Validates that an entry can fit in cache.
        /// </summary>
        public bool CanFitEntry(long entrySizeBytes)
        {
            return entrySizeBytes <= MaxEntrySizeBytes && entrySizeBytes <= MaxTotalSizeBytes;
        }

        /// <summary>
        /// Calculates total size needed to add new entry.
        /// </summary>
        public long GetSizeAfterAdd(long currentSizeBytes, long newEntrySizeBytes)
        {
            return currentSizeBytes + newEntrySizeBytes;
        }
    }

    /// <summary>
    /// Eviction strategy that selects entries to remove based on LRU algorithm.
    /// </summary>
    public class LRUEvictionStrategy
    {
        /// <summary>
        /// Selects entries to evict using Least Recently Used algorithm.
        /// Returns entries sorted from least to most important (first to last to evict).
        /// </summary>
        public static List<CacheEntry> SelectForEviction(
            List<CacheEntry> allEntries,
            long targetFreeBytes,
            CacheEvictionPolicy policy)
        {
            if (allEntries == null || allEntries.Count == 0)
                return new List<CacheEntry>();

            var selectedForEviction = new List<CacheEntry>();
            long freedBytes = 0;

            // First, remove expired entries (no cost)
            foreach (var entry in allEntries.Where(e => policy.IsExpired(e)).OrderBy(e => e.LastAccessedAt))
            {
                selectedForEviction.Add(entry);
                freedBytes += entry.SizeBytes;

                if (freedBytes >= targetFreeBytes)
                    return selectedForEviction;
            }

            // Then, remove LRU entries (least recently accessed first)
            foreach (var entry in allEntries
                .Where(e => !policy.IsExpired(e))
                .OrderBy(e => e.LastAccessedAt))
            {
                selectedForEviction.Add(entry);
                freedBytes += entry.SizeBytes;

                if (freedBytes >= targetFreeBytes)
                    break;
            }

            return selectedForEviction;
        }

        /// <summary>
        /// Calculates total size of entries to be evicted.
        /// </summary>
        public static long GetEvictionSize(List<CacheEntry> selectedEntries)
        {
            return selectedEntries?.Sum(e => e.SizeBytes) ?? 0;
        }
    }
}
