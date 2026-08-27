// AgenticAI.ContextEngineering.Core/Caching/TokenCache.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    public class TokenCache : ITokenCache
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<TokenCache> _logger;
        private readonly MemoryCacheEntryOptions _defaultOptions;

        // Statistics tracking
        private long _cacheHits;
        private long _cacheMisses;
        private long _totalTokensCached;
        private long _totalTokensSaved;
        private long _totalCostSaved;

        private readonly ConcurrentDictionary<string, TokenCacheEntry> _cacheEntries = new();

        // Separate cache for token counts
        private readonly IMemoryCache _tokenCountCache;

        public TokenCache(
            IMemoryCache memoryCache,
            ILogger<TokenCache> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _defaultOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
                SlidingExpiration = TimeSpan.FromHours(1),
                Priority = CacheItemPriority.Normal
            };

            // Separate cache for token counts with shorter expiration
            _tokenCountCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = 1000,
                ExpirationScanFrequency = TimeSpan.FromMinutes(5)
            });
        }

        public async Task<T?> GetOrAddAsync<T>(
            string key,
            Func<Task<T>> factory,
            int? tokenCount = null,
            decimal? costPerToken = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (_memoryCache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
            {
                Interlocked.Increment(ref _cacheHits);

                if (tokenCount.HasValue)
                {
                    Interlocked.Add(ref _totalTokensSaved, tokenCount.Value);

                    if (costPerToken.HasValue)
                    {
                        Interlocked.Add(ref _totalCostSaved, (long)(tokenCount.Value * costPerToken.Value * 1000000m));
                    }

                    if (_cacheEntries.TryGetValue(key, out var entry))
                    {
                        entry.HitCount++;
                        entry.LastHitTime = DateTime.UtcNow;
                    }
                }

                _logger.LogDebug($"✅ Token cache HIT: {key}");
                return cachedValue;
            }

            Interlocked.Increment(ref _cacheMisses);
            _logger.LogDebug($"❌ Token cache MISS: {key}");

            var result = await factory();

            if (result != null)
            {
                _memoryCache.Set(key, result, _defaultOptions);

                if (tokenCount.HasValue)
                {
                    Interlocked.Add(ref _totalTokensCached, tokenCount.Value);

                    _cacheEntries[key] = new TokenCacheEntry
                    {
                        Key = key,
                        TokenCount = tokenCount.Value,
                        CreatedAt = DateTime.UtcNow,
                        HitCount = 0
                    };
                }

                _logger.LogDebug($"✅ Token cache SET: {key}");
            }

            return result;
        }

        public bool TryGet<T>(string key, out T? value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (_memoryCache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
            {
                value = cachedValue;
                Interlocked.Increment(ref _cacheHits);

                if (_cacheEntries.TryGetValue(key, out var entry))
                {
                    entry.HitCount++;
                    entry.LastHitTime = DateTime.UtcNow;
                    Interlocked.Add(ref _totalTokensSaved, entry.TokenCount);
                }

                return true;
            }

            value = default;
            Interlocked.Increment(ref _cacheMisses);
            return false;
        }

        public void Set<T>(string key, T value, int? tokenCount = null, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            var options = expiration.HasValue
                ? new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration.Value,
                    SlidingExpiration = TimeSpan.FromMinutes(30),
                    Priority = CacheItemPriority.Normal
                }
                : _defaultOptions;

            _memoryCache.Set(key, value, options);

            if (tokenCount.HasValue)
            {
                Interlocked.Add(ref _totalTokensCached, tokenCount.Value);

                _cacheEntries[key] = new TokenCacheEntry
                {
                    Key = key,
                    TokenCount = tokenCount.Value,
                    CreatedAt = DateTime.UtcNow,
                    HitCount = 0
                };
            }
        }

        public void Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _memoryCache.Remove(key);
            _cacheEntries.TryRemove(key, out _);
            _logger.LogDebug($"🗑️ Token cache removed: {key}");
        }

        public void Clear()
        {
            foreach (var key in _cacheEntries.Keys)
            {
                _memoryCache.Remove(key);
            }

            _cacheEntries.Clear();

            Interlocked.Exchange(ref _cacheHits, 0);
            Interlocked.Exchange(ref _cacheMisses, 0);
            Interlocked.Exchange(ref _totalTokensCached, 0);
            Interlocked.Exchange(ref _totalTokensSaved, 0);
            Interlocked.Exchange(ref _totalCostSaved, 0);

            _logger.LogInformation("🗑️ Token cache cleared");
        }

        public TokenUsageStats GetStats()
        {
            var hits = Interlocked.Read(ref _cacheHits);
            var misses = Interlocked.Read(ref _cacheMisses);
            var totalRequests = hits + misses;

            return new TokenUsageStats
            {
                CacheHits = hits,
                CacheMisses = misses,
                TotalTokensCached = Interlocked.Read(ref _totalTokensCached),
                TotalTokensSaved = Interlocked.Read(ref _totalTokensSaved),
                TotalCostSaved = Interlocked.Read(ref _totalCostSaved) / 1000000m,
                CacheHitRate = totalRequests > 0 ? (double)hits / totalRequests : 0,
                CacheEntryCount = _cacheEntries.Count
            };
        }

        public async Task<string> GenerateReportAsync()
        {
            var stats = GetStats();

            return $@"
══════════════════════════════════════════════════
📊 TOKEN CACHE REPORT
══════════════════════════════════════════════════
  Cache Hits:        {stats.CacheHits:N0}
  Cache Misses:      {stats.CacheMisses:N0}
  Hit Rate:          {stats.CacheHitRate:P2}
  Tokens Cached:     {stats.TotalTokensCached:N0}
  Tokens Saved:      {stats.TotalTokensSaved:N0}
  Cost Saved:        ${stats.TotalCostSaved:F4}
  Cache Entries:     {stats.CacheEntryCount:N0}
══════════════════════════════════════════════════";
        }

        // ============================================================
        // NEW METHODS FOR TOKEN COUNTING (used by SearchService)
        // ============================================================

        public async Task<int> GetCachedTokenCountAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                return 0;

            if (_tokenCountCache.TryGetValue(key, out int cachedCount))
            {
                return cachedCount;
            }

            return 0;
        }

        public async Task CacheTokenCountAsync(string key, int count, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(key))
                return;

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromHours(24),
                SlidingExpiration = TimeSpan.FromHours(1),
                Priority = CacheItemPriority.Normal,
                Size = 1
            };

            _tokenCountCache.Set(key, count, options);
        }

        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Simple token estimation:
            // - Split by spaces and punctuation
            // - Approximate 1 token ≈ 4 characters for English text
            // - This is a rough estimate, not exact

            // Remove extra whitespace
            var cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            cleaned = cleaned.Trim();

            if (string.IsNullOrEmpty(cleaned))
                return 0;

            // Count words
            var words = cleaned.Split(new[] { ' ', '.', ',', '!', '?', ';', ':', '\n', '\r' },
                                      StringSplitOptions.RemoveEmptyEntries);

            // Approximate tokens: words * 1.3 (average tokens per word in English)
            // Plus punctuation and special characters
            var estimatedTokens = (int)Math.Ceiling(words.Length * 1.3);

            // Add extra for numbers, symbols, and special characters
            var specialChars = 0;
            foreach (char c in cleaned)
            {
                if (!char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
                    specialChars++;
            }
            estimatedTokens += specialChars / 2;

            // Minimum 1 token
            return Math.Max(1, estimatedTokens);
        }

        private class TokenCacheEntry
        {
            public string Key { get; set; } = string.Empty;
            public int TokenCount { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? LastHitTime { get; set; }
            public long HitCount { get; set; }
        }
    }
}