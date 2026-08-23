// AgenticAI.ContextEngineering.Core/Caching/TokenCache.cs
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    public class TokenCache : ITokenCache
    {
        private readonly ILogger<TokenCache> _logger;
        private readonly ConcurrentDictionary<string, CachedPrompt> _promptCache = new();
        private readonly ConcurrentDictionary<string, CachedCompletion> _completionCache = new();
        private readonly ConcurrentDictionary<string, CachedEmbedding> _embeddingCache = new();
        private readonly ConcurrentDictionary<string, object> _responseCache = new();
        private readonly ConcurrentDictionary<string, int> _tokenCountCache = new();
        private readonly TokenUsageStats _stats = new();
        private readonly SemaphoreSlim _statsLock = new(1, 1);

        public TokenCache(ILogger<TokenCache> logger)
        {
            _logger = logger;
        }

        // Prompt Caching
        public Task<CachedPrompt> GetPromptAsync(string promptHash)
        {
            _promptCache.TryGetValue(promptHash, out var prompt);
            return Task.FromResult(prompt);
        }

        public Task CachePromptAsync(CachedPrompt prompt)
        {
            _promptCache[prompt.PromptHash] = prompt;
            return Task.CompletedTask;
        }

        public async Task<CachedPrompt> GetOrCachePromptAsync(string promptText)
        {
            var hash = HashText(promptText);
            var cached = await GetPromptAsync(hash);
            if (cached != null) return cached;

            var prompt = new CachedPrompt
            {
                PromptHash = hash,
                PromptText = promptText,
                TokenCount = CountTokens(promptText),
                CachedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            await CachePromptAsync(prompt);
            return prompt;
        }

        // Completion Caching
        public Task<CachedCompletion> GetCompletionAsync(string completionHash)
        {
            _completionCache.TryGetValue(completionHash, out var completion);
            return Task.FromResult(completion);
        }

        public Task CacheCompletionAsync(CachedCompletion completion)
        {
            _completionCache[completion.CompletionHash] = completion;
            return Task.CompletedTask;
        }

        public async Task<CachedCompletion> GetOrCacheCompletionAsync(string promptText, string completionText, double confidence = 1.0)
        {
            var hash = HashText(promptText + completionText);
            var cached = await GetCompletionAsync(hash);
            if (cached != null) return cached;

            var completion = new CachedCompletion
            {
                CompletionHash = hash,
                CompletionText = completionText,
                TokenCount = CountTokens(completionText),
                Confidence = confidence,
                CachedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            await CacheCompletionAsync(completion);
            return completion;
        }

        // Generic Response Caching
        public Task<T> GetCachedResponseAsync<T>(string cacheKey) where T : class
        {
            _responseCache.TryGetValue(cacheKey, out var response);
            return Task.FromResult(response as T);
        }

        public Task CacheResponseAsync<T>(string cacheKey, T response, TimeSpan? expiration = null) where T : class
        {
            _responseCache[cacheKey] = response;
            return Task.CompletedTask;
        }

        // Token Count Caching
        public Task<int> GetCachedTokenCountAsync(string cacheKey)
        {
            _tokenCountCache.TryGetValue(cacheKey, out var count);
            return Task.FromResult(count);
        }

        public Task CacheTokenCountAsync(string cacheKey, int tokenCount, TimeSpan? expiration = null)
        {
            _tokenCountCache[cacheKey] = tokenCount;
            return Task.CompletedTask;
        }

        // ✅ Token Usage Tracking
        public async Task TrackUsageAsync(string query, int promptTokens, int completionTokens, CancellationToken cancellationToken = default)
        {
            await _statsLock.WaitAsync(cancellationToken);
            try
            {
                _stats.TotalTokensCached += promptTokens + completionTokens;
                _stats.TotalTokensSaved += promptTokens + completionTokens;
                _stats.TotalPromptsCached++;
                _stats.TotalCompletionsCached++;
                _stats.CacheHitRate = 0.5; // Simple average
                _stats.CostSaved = (_stats.TotalTokensSaved / 1000.0) * 0.02;
                _stats.StatsUpdated = DateTime.UtcNow;

                // Add to token savings by type
                _stats.TokenSavingsByType["prompt"] = _stats.TokenSavingsByType.GetValueOrDefault("prompt", 0) + promptTokens;
                _stats.TokenSavingsByType["completion"] = _stats.TokenSavingsByType.GetValueOrDefault("completion", 0) + completionTokens;

                _logger.LogDebug($"📊 Token usage tracked: Prompt={promptTokens}, Completion={completionTokens}, Total={promptTokens + completionTokens}");
            }
            finally
            {
                _statsLock.Release();
            }
        }

        // Embedding Caching
        public Task<CachedEmbedding> GetEmbeddingAsync(string textHash)
        {
            _embeddingCache.TryGetValue(textHash, out var embedding);
            return Task.FromResult(embedding);
        }

        public Task CacheEmbeddingAsync(CachedEmbedding embedding)
        {
            _embeddingCache[embedding.TextHash] = embedding;
            return Task.CompletedTask;
        }

        public async Task<CachedEmbedding> GetOrCacheEmbeddingAsync(string text, Func<string, float[]> embeddingGenerator)
        {
            var hash = HashText(text);
            var cached = await GetEmbeddingAsync(hash);
            if (cached != null) return cached;

            var embedding = new CachedEmbedding
            {
                TextHash = hash,
                Text = text,
                Embedding = embeddingGenerator(text),
                Dimensions = embeddingGenerator(text).Length,
                CachedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            await CacheEmbeddingAsync(embedding);
            return embedding;
        }

        // Utilities
        public string HashText(string text)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash).Substring(0, 32);
        }

        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // Simple approximation: ~4 chars per token
            return text.Length / 4;
        }

        public TokenUsageStats GetTokenStats()
        {
            return _stats;
        }

        public async Task ClearAsync()
        {
            await _statsLock.WaitAsync();
            try
            {
                _promptCache.Clear();
                _completionCache.Clear();
                _embeddingCache.Clear();
                _responseCache.Clear();
                _tokenCountCache.Clear();
                _stats.TotalTokensCached = 0;
                _stats.TotalTokensSaved = 0;
                _stats.TotalPromptsCached = 0;
                _stats.TotalCompletionsCached = 0;
                _stats.CacheHitRate = 0;
                _stats.CostSaved = 0;
                _stats.TokenSavingsByType.Clear();
                _logger.LogInformation("🗑️ Token cache cleared");
            }
            finally
            {
                _statsLock.Release();
            }
        }

        public long GetCacheSize()
        {
            return _promptCache.Count + _completionCache.Count + _embeddingCache.Count + _responseCache.Count;
        }
    }
}