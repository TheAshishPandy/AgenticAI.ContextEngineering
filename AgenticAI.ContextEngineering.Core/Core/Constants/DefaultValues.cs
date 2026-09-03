// AgenticAI.ContextEngineering.Core/Core/Constants/DefaultValues.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Constants
{
    /// <summary>
    /// Default configuration values for the search engine and related services.
    /// These constants represent production-safe defaults and can be overridden via configuration.
    /// </summary>
    public static class DefaultValues
    {
        // ============================================================
        // 🧠 EMBEDDING CONFIGURATION
        // ============================================================

        /// <summary>
        /// Default embedding vector dimension (Azure OpenAI text-embedding-3-small).
        /// </summary>
        public const int EmbeddingDimension = 1536;

        /// <summary>
        /// Default embedding model (Azure OpenAI).
        /// </summary>
        public const string EmbeddingModel = "text-embedding-3-small";

        // ============================================================
        // ⏱️ RATE LIMITING
        // ============================================================

        /// <summary>
        /// Default maximum concurrent embedding generation requests.
        /// </summary>
        public const int EmbeddingConcurrentRequests = 5;

        /// <summary>
        /// Default maximum embedding requests per minute (Azure OpenAI tier-1 limit).
        /// </summary>
        public const int EmbeddingRequestsPerMinute = 100;

        /// <summary>
        /// Default maximum requests per user per minute.
        /// </summary>
        public const int UserRequestsPerMinute = 100;

        /// <summary>
        /// Default timeout for embedding generation in seconds.
        /// </summary>
        public const int EmbeddingTimeoutSeconds = 30;

        // ============================================================
        // 🔍 SEARCH CONFIGURATION
        // ============================================================

        /// <summary>
        /// Default number of top results to return from search.
        /// </summary>
        public const int DefaultTopResults = 10;

        /// <summary>
        /// Maximum allowed top results in a single search request.
        /// </summary>
        public const int MaxTopResults = 1000;

        /// <summary>
        /// Default minimum relevance score threshold (0.0 - 1.0).
        /// </summary>
        public const double DefaultMinimumRelevanceScore = 0.3;

        /// <summary>
        /// Minimum allowed relevance score.
        /// </summary>
        public const double MinimumRelevanceScore = 0.0;

        /// <summary>
        /// Maximum allowed relevance score.
        /// </summary>
        public const double MaximumRelevanceScore = 1.0;

        // ============================================================
        // 💾 CACHING
        // ============================================================

        /// <summary>
        /// Default cache expiration time in hours.
        /// </summary>
        public const int CacheExpirationHours = 1;

        /// <summary>
        /// Maximum total cache size in bytes (500 MB).
        /// </summary>
        public const long MaxCacheSizeBytes = 500L * 1024 * 1024;

        /// <summary>
        /// Maximum individual cache entry size in bytes (50 MB).
        /// </summary>
        public const long MaxCacheEntrySizeBytes = 50L * 1024 * 1024;

        /// <summary>
        /// Cache eviction threshold percentage (evict at 80% full).
        /// </summary>
        public const int CacheEvictionThresholdPercent = 80;

        // ============================================================
        // 🔐 SECURITY & VALIDATION
        // ============================================================

        /// <summary>
        /// Maximum query length in characters to prevent DoS.
        /// </summary>
        public const int MaxQueryLength = 10000;

        /// <summary>
        /// Minimum query length in characters.
        /// </summary>
        public const int MinQueryLength = 1;

        /// <summary>
        /// Maximum batch size for bulk operations.
        /// </summary>
        public const int MaxBatchSize = 1000;

        /// <summary>
        /// Default batch size for processing.
        /// </summary>
        public const int DefaultBatchSize = 100;

        // ============================================================
        // 📊 RETRY & RESILIENCE
        // ============================================================

        /// <summary>
        /// Default number of retry attempts for transient failures.
        /// </summary>
        public const int DefaultRetryAttempts = 3;

        /// <summary>
        /// Default initial retry delay in milliseconds (exponential backoff).
        /// </summary>
        public const int RetryInitialDelayMs = 100;

        /// <summary>
        /// Maximum retry delay in milliseconds.
        /// </summary>
        public const int RetryMaxDelayMs = 30000;

        /// <summary>
        /// Exponential backoff multiplier for retries.
        /// </summary>
        public const double RetryBackoffMultiplier = 2.0;

        // ============================================================
        // 📝 LOGGING & DIAGNOSTICS
        // ============================================================

        /// <summary>
        /// Whether to log sensitive user queries (set to false in production).
        /// </summary>
        public const bool LogSensitiveQueries = false;

        /// <summary>
        /// Whether to include detailed performance metrics in logs.
        /// </summary>
        public const bool IncludePerformanceMetrics = true;

        // ============================================================
        // 🎯 FEATURE FLAGS
        // ============================================================

        /// <summary>
        /// Enable vector dimension validation on all operations.
        /// </summary>
        public const bool EnableVectorDimensionValidation = true;

        /// <summary>
        /// Enable automatic cache eviction when limit reached.
        /// </summary>
        public const bool EnableAutoCacheEviction = true;

        /// <summary>
        /// Enable request-level rate limiting per user.
        /// </summary>
        public const bool EnablePerUserRateLimiting = true;

        /// <summary>
        /// Enable audit logging of all search operations.
        /// </summary>
        public const bool EnableAuditLogging = true;
    }
}
