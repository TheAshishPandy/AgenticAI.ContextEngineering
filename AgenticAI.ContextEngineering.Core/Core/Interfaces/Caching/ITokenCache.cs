// AgenticAI.ContextEngineering.Core/Interfaces/ITokenCache.cs
using AgenticAI.ContextEngineering.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    public interface ITokenCache
    {
        /// <summary>
        /// Get or add a value to the cache with token tracking
        /// </summary>
        Task<T?> GetOrAddAsync<T>(
            string key,
            Func<Task<T>> factory,
            int? tokenCount = null,
            decimal? costPerToken = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Try to get a value from the cache
        /// </summary>
        bool TryGet<T>(string key, out T? value);

        /// <summary>
        /// Set a value in the cache
        /// </summary>
        void Set<T>(string key, T value, int? tokenCount = null, TimeSpan? expiration = null);

        /// <summary>
        /// Remove a value from the cache
        /// </summary>
        void Remove(string key);

        /// <summary>
        /// Clear all cache entries
        /// </summary>
        void Clear();

        /// <summary>
        /// Get cache statistics
        /// </summary>
        TokenUsageStats GetStats();

        /// <summary>
        /// Generate a cache report
        /// </summary>
        Task<string> GenerateReportAsync();

        // ============================================================
        // NEW METHODS FOR TOKEN COUNTING (used by SearchService)
        // ============================================================

        /// <summary>
        /// Get cached token count for a key
        /// </summary>
        Task<int> GetCachedTokenCountAsync(string key);

        /// <summary>
        /// Cache a token count
        /// </summary>
        Task CacheTokenCountAsync(string key, int count, TimeSpan? expiration = null);

        /// <summary>
        /// Count tokens in a string (simple estimation)
        /// </summary>
        int CountTokens(string text);
    }
}