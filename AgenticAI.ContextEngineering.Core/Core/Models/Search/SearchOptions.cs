// Core/Models/SearchOptions.cs
using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class SearchOptions
    {
        // ============================================================
        // 🔍 AZURE AI SEARCH CONFIGURATION
        // ============================================================
        public string? AzureSearchEndpoint { get; set; }
        public string? AzureSearchKey { get; set; }
        public string? AzureSearchIndexName { get; set; }
        public string? AzureSearchSemanticConfigName { get; set; } = "default";

        // ============================================================
        // 📚 SEARCH BEHAVIOR
        // ============================================================
        public int DefaultTopK { get; set; } = 10;
        public int TopK { get; set; } = 10;
        public int MaxResults { get; set; } = 100;
        public int BatchSize { get; set; } = 100;
        public double MinimumRelevanceScore { get; set; } = 0.3;
        public double MinimumConfidenceThreshold { get; set; } = 0.3;
        public bool EnableReranking { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
        public bool AutoIndex { get; set; } = true;
        public bool AutoLoadAtStartup { get; set; } = true;
        public int CacheExpirationHours { get; set; } = 1;

        // ============================================================
        // 🔍 SEARCH MODE
        // ============================================================
        public SearchMode DefaultSearchMode { get; set; } = SearchMode.Hybrid;
        public bool UseSemanticSearch { get; set; } = true;
        public bool UseLexicalSearch { get; set; } = true;
        public bool UseHybridSearch { get; set; } = true;

        // ============================================================
        // 🧮 BM25 PARAMETERS
        // ============================================================
        public double BM25K1 { get; set; } = 1.2;
        public double BM25B { get; set; } = 0.75;
        public double BM25MinDocumentFrequency { get; set; } = 1;
        public double BM25MaxDocumentFrequency { get; set; } = 0.8;

        // ============================================================
        // 🔀 HYBRID SEARCH PARAMETERS
        // ============================================================
        public int RRF_K { get; set; } = 60;
        public double SemanticWeight { get; set; } = 0.6;
        public double LexicalWeight { get; set; } = 0.4;
        public double MinHybridScore { get; set; } = 0.3;

        // ============================================================
        // 🔢 QUERY EXPANSION
        // ============================================================
        public bool EnableQueryExpansion { get; set; } = true;
        public int MaxQueryVariants { get; set; } = 5;
        public double QueryExpansionBoost { get; set; } = 0.9;

        // ============================================================
        // 🧠 EMBEDDING SETTINGS
        // ============================================================
        public EmbeddingBackend EmbeddingBackend { get; set; } = EmbeddingBackend.Mock;
        public int EmbeddingDimensions { get; set; } = 384;
        public int EmbeddingBatchSize { get; set; } = 10;
        public string? EmbeddingModelName { get; set; } = "text-embedding-ada-002";
        public string? EmbeddingDeploymentName { get; set; }
        public string? EmbeddingEndpoint { get; set; }
        public string? EmbeddingApiKey { get; set; }
        public bool UseCachedEmbeddings { get; set; } = true;
        public int EmbeddingCacheExpirationHours { get; set; } = 24;

        // ============================================================
        // 🗄️ QDRANT CONFIGURATION
        // ============================================================
        public string? QdrantHost { get; set; }
        public string? QdrantApiKey { get; set; }
        public string? QdrantCollectionName { get; set; } = "documents";
        public int QdrantVectorSize { get; set; } = 1536;
        public string? QdrantDistance { get; set; } = "Cosine";

        // ============================================================
        // ⏰ TIMEOUT & RETRY
        // ============================================================
        public int SearchTimeoutSeconds { get; set; } = 30;
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;

        // ============================================================
        // 📊 FILTERING
        // ============================================================
        public bool EnableFiltering { get; set; } = true;
        public List<string>? DefaultFilters { get; set; }
        public Dictionary<string, object>? FilterDefaults { get; set; }

        // ============================================================
        // 🧪 FEATURE FLAGS
        // ============================================================
        public bool EnableLogging { get; set; } = true;
        public bool EnableMetrics { get; set; } = true;
        public bool EnableDetailedLogging { get; set; } = false;
        public bool EnableDebugMode { get; set; } = false;

        // ============================================================
        // 📦 CONTEXTUAL SEARCH
        // ============================================================
        public bool EnableContextualSearch { get; set; } = true;
        public int ContextWindowSize { get; set; } = 5;
        public double ContextBoost { get; set; } = 0.2;

        // ============================================================
        // 🎯 RELEVANCE OPTIMIZATION
        // ============================================================
        public bool EnableRelevanceOptimization { get; set; } = true;
        public double RelevanceScoreThreshold { get; set; } = 0.3;
        public double HighRelevanceThreshold { get; set; } = 0.8;
        public double MediumRelevanceThreshold { get; set; } = 0.5;

        // ============================================================
        // 🏷️ TAGS & CATEGORIES
        // ============================================================
        public bool EnableTagFiltering { get; set; } = true;
        public bool EnableCategoryFiltering { get; set; } = true;
        public bool EnableLanguageFiltering { get; set; } = true;

        // ============================================================
        // 🎯 SEARCH RESULT FORMATTING
        // ============================================================
        public bool IncludeScoreBreakdown { get; set; } = true;
        public bool IncludeMetadata { get; set; } = true;
        public bool IncludeSource { get; set; } = true;
        public bool IncludeHighlighting { get; set; } = true;
        public int MaxHighlightLength { get; set; } = 200;

        // ============================================================
        // 🔗 CROSS-LINGUAL SUPPORT
        // ============================================================
        public bool EnableCrossLingualSearch { get; set; } = false;
        public List<int> SupportedLanguages { get; set; } = new() { 1, 2, 3, 4, 5 };
        public int DefaultLanguage { get; set; } = 1;

        // ============================================================
        // 🧹 CLEANUP & MAINTENANCE
        // ============================================================
        public bool AutoCleanupIndex { get; set; } = true;
        public int CleanupIntervalHours { get; set; } = 24;
        public int MaxIndexSizeMB { get; set; } = 1024;
        public bool EnableCompression { get; set; } = true;

        // ============================================================
        // 🧪 HELPER METHODS
        // ============================================================
        public SearchOptions Clone()
        {
            return new SearchOptions
            {
                // Azure Search
                AzureSearchEndpoint = AzureSearchEndpoint,
                AzureSearchKey = AzureSearchKey,
                AzureSearchIndexName = AzureSearchIndexName,
                AzureSearchSemanticConfigName = AzureSearchSemanticConfigName,

                // Search Behavior
                DefaultTopK = DefaultTopK,
                TopK = TopK,
                MaxResults = MaxResults,
                BatchSize = BatchSize,
                MinimumRelevanceScore = MinimumRelevanceScore,
                MinimumConfidenceThreshold = MinimumConfidenceThreshold,
                EnableReranking = EnableReranking,
                EnableCaching = EnableCaching,
                AutoIndex = AutoIndex,
                AutoLoadAtStartup = AutoLoadAtStartup,
                CacheExpirationHours = CacheExpirationHours,

                // Search Mode
                DefaultSearchMode = DefaultSearchMode,
                UseSemanticSearch = UseSemanticSearch,
                UseLexicalSearch = UseLexicalSearch,
                UseHybridSearch = UseHybridSearch,

                // BM25
                BM25K1 = BM25K1,
                BM25B = BM25B,
                BM25MinDocumentFrequency = BM25MinDocumentFrequency,
                BM25MaxDocumentFrequency = BM25MaxDocumentFrequency,

                // Hybrid Search
                RRF_K = RRF_K,
                SemanticWeight = SemanticWeight,
                LexicalWeight = LexicalWeight,
                MinHybridScore = MinHybridScore,

                // Query Expansion
                EnableQueryExpansion = EnableQueryExpansion,
                MaxQueryVariants = MaxQueryVariants,
                QueryExpansionBoost = QueryExpansionBoost,

                // Embedding
                EmbeddingBackend = EmbeddingBackend,
                EmbeddingDimensions = EmbeddingDimensions,
                EmbeddingBatchSize = EmbeddingBatchSize,
                EmbeddingModelName = EmbeddingModelName,
                EmbeddingDeploymentName = EmbeddingDeploymentName,
                EmbeddingEndpoint = EmbeddingEndpoint,
                EmbeddingApiKey = EmbeddingApiKey,
                UseCachedEmbeddings = UseCachedEmbeddings,
                EmbeddingCacheExpirationHours = EmbeddingCacheExpirationHours,

                // Qdrant
                QdrantHost = QdrantHost,
                QdrantApiKey = QdrantApiKey,
                QdrantCollectionName = QdrantCollectionName,
                QdrantVectorSize = QdrantVectorSize,
                QdrantDistance = QdrantDistance,

                // Timeout & Retry
                SearchTimeoutSeconds = SearchTimeoutSeconds,
                MaxRetryAttempts = MaxRetryAttempts,
                RetryDelayMilliseconds = RetryDelayMilliseconds,

                // Filtering
                EnableFiltering = EnableFiltering,
                DefaultFilters = DefaultFilters != null ? new List<string>(DefaultFilters) : null,
                FilterDefaults = FilterDefaults != null ? new Dictionary<string, object>(FilterDefaults) : null,

                // Feature Flags
                EnableLogging = EnableLogging,
                EnableMetrics = EnableMetrics,
                EnableDetailedLogging = EnableDetailedLogging,
                EnableDebugMode = EnableDebugMode,

                // Contextual Search
                EnableContextualSearch = EnableContextualSearch,
                ContextWindowSize = ContextWindowSize,
                ContextBoost = ContextBoost,

                // Relevance Optimization
                EnableRelevanceOptimization = EnableRelevanceOptimization,
                RelevanceScoreThreshold = RelevanceScoreThreshold,
                HighRelevanceThreshold = HighRelevanceThreshold,
                MediumRelevanceThreshold = MediumRelevanceThreshold,

                // Tags & Categories
                EnableTagFiltering = EnableTagFiltering,
                EnableCategoryFiltering = EnableCategoryFiltering,
                EnableLanguageFiltering = EnableLanguageFiltering,

                // Result Formatting
                IncludeScoreBreakdown = IncludeScoreBreakdown,
                IncludeMetadata = IncludeMetadata,
                IncludeSource = IncludeSource,
                IncludeHighlighting = IncludeHighlighting,
                MaxHighlightLength = MaxHighlightLength,

                // Cross-Lingual
                EnableCrossLingualSearch = EnableCrossLingualSearch,
                SupportedLanguages = new List<int>(SupportedLanguages),
                DefaultLanguage = DefaultLanguage,

                // Cleanup
                AutoCleanupIndex = AutoCleanupIndex,
                CleanupIntervalHours = CleanupIntervalHours,
                MaxIndexSizeMB = MaxIndexSizeMB,
                EnableCompression = EnableCompression
            };
        }

        /// <summary>
        /// Get search mode based on configuration
        /// </summary>
        public SearchMode GetEffectiveSearchMode()
        {
            if (UseHybridSearch)
                return SearchMode.Hybrid;
            if (UseSemanticSearch)
                return SearchMode.Semantic;
            if (UseLexicalSearch)
                return SearchMode.Lexical;
            return SearchMode.Hybrid;
        }

        /// <summary>
        /// Check if Qdrant is configured
        /// </summary>
        public bool IsQdrantConfigured()
        {
            return !string.IsNullOrEmpty(QdrantHost) && !string.IsNullOrEmpty(QdrantApiKey);
        }

        /// <summary>
        /// Check if Azure Search is configured
        /// </summary>
        public bool IsAzureSearchConfigured()
        {
            return !string.IsNullOrEmpty(AzureSearchEndpoint) &&
                   !string.IsNullOrEmpty(AzureSearchKey) &&
                   !string.IsNullOrEmpty(AzureSearchIndexName);
        }

        /// <summary>
        /// Check if embedding is configured
        /// </summary>
        public bool IsEmbeddingConfigured()
        {
            return EmbeddingBackend != EmbeddingBackend.Mock &&
                   !string.IsNullOrEmpty(EmbeddingEndpoint) &&
                   !string.IsNullOrEmpty(EmbeddingApiKey);
        }

        /// <summary>
        /// Get the appropriate embedding model name
        /// </summary>
        public string GetEmbeddingModelName()
        {
            return EmbeddingDeploymentName ?? EmbeddingModelName ?? "text-embedding-ada-002";
        }
    }

    /// <summary>
    /// Search mode types
    /// </summary>
    public enum SearchMode
    {
        Lexical = 0,
        Semantic = 1,
        Hybrid = 2,
        Adaptive = 3
    }

    /// <summary>
    /// Embedding backend types
    /// </summary>
    public enum EmbeddingBackend
    {
        AzureOpenAI = 0,
        SentenceTransformers = 1,
        Qdrant = 2,
        Mock = 3,
        Hash = 4
    }
}