// AgenticAI.ContextEngineering.Core/Caching/ITokenCache.cs
using AgenticAI.ContextEngineering.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    public interface ITokenCache
    {
        // Prompt Caching
        Task<CachedPrompt> GetPromptAsync(string promptHash);
        Task CachePromptAsync(CachedPrompt prompt);
        Task<CachedPrompt> GetOrCachePromptAsync(string promptText);

        // Completion Caching
        Task<CachedCompletion> GetCompletionAsync(string completionHash);
        Task CacheCompletionAsync(CachedCompletion completion);
        Task<CachedCompletion> GetOrCacheCompletionAsync(string promptText, string completionText, double confidence = 1.0);

        // Generic Response Caching
        Task<T> GetCachedResponseAsync<T>(string cacheKey) where T : class;
        Task CacheResponseAsync<T>(string cacheKey, T response, TimeSpan? expiration = null) where T : class;

        // Token Count Caching
        Task<int> GetCachedTokenCountAsync(string cacheKey);
        Task CacheTokenCountAsync(string cacheKey, int tokenCount, TimeSpan? expiration = null);

        // ✅ Token Usage Tracking
        Task TrackUsageAsync(string query, int promptTokens, int completionTokens, CancellationToken cancellationToken = default);

        // Embedding Caching
        Task<CachedEmbedding> GetEmbeddingAsync(string textHash);
        Task CacheEmbeddingAsync(CachedEmbedding embedding);
        Task<CachedEmbedding> GetOrCacheEmbeddingAsync(string text, Func<string, float[]> embeddingGenerator);

        // Utilities
        string HashText(string text);
        int CountTokens(string text);
        TokenUsageStats GetTokenStats();
        Task ClearAsync();
        long GetCacheSize();
    }

    public class CachedPrompt
    {
        public string PromptHash { get; set; } = string.Empty;
        public string PromptText { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }

    public class CachedCompletion
    {
        public string CompletionHash { get; set; } = string.Empty;
        public string CompletionText { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public double Confidence { get; set; } = 1.0;
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }

    public class CachedEmbedding
    {
        public string TextHash { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public int Dimensions { get; set; }
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }
}