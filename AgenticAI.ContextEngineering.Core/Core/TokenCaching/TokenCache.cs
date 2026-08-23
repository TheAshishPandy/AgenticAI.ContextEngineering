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
        private readonly ConcurrentDictionary<string, object> _cache = new();
        private readonly TokenUsageStats _stats = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public TokenCache(ILogger<TokenCache> logger)
        {
            _logger = logger;
        }

        public async Task<T> GetCachedResponseAsync<T>(string cacheKey) where T : class
        {
            try
            {
                if (_cache.TryGetValue(cacheKey, out var value) && value is T typedValue)
                {
                    _logger.LogDebug($"✅ Token Cache HIT: {cacheKey}");
                    return typedValue;
                }
                _logger.LogDebug($"❌ Token Cache MISS: {cacheKey}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving from token cache");
            }

            return await Task.FromResult<T>(null);
        }

        public async Task CacheResponseAsync<T>(string cacheKey, T response, TimeSpan? expiration = null) where T : class
        {
            try
            {
                _cache[cacheKey] = response;
                _stats.TotalTokensCached++;
                _logger.LogDebug($"✅ Token Cache SET: {cacheKey}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error saving to token cache");
            }

            await Task.CompletedTask;
        }

        public async Task TrackUsageAsync(string query, int promptTokens, int completionTokens, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                var totalTokens = promptTokens + completionTokens;
                _stats.TotalTokensCached += totalTokens;
                _stats.TotalTokensSaved += totalTokens;
                _stats.TotalPromptsCached++;
                _stats.TotalCompletionsCached++;
                _stats.CacheHitRate = CalculateHitRate();
                _stats.CostSaved = (_stats.TotalTokensSaved / 1000.0) * 0.02;
                _stats.StatsUpdated = DateTime.UtcNow;

                // Track by type
                _stats.TokenSavingsByType["prompt"] = _stats.TokenSavingsByType.GetValueOrDefault("prompt", 0) + promptTokens;
                _stats.TokenSavingsByType["completion"] = _stats.TokenSavingsByType.GetValueOrDefault("completion", 0) + completionTokens;
                _stats.TokenSavingsByType["total"] = _stats.TokenSavingsByType.GetValueOrDefault("total", 0) + totalTokens;

                _logger.LogDebug($"📊 Token usage tracked: {totalTokens} tokens");
            }
            finally
            {
                _lock.Release();
            }
        }

        public TokenUsageStats GetTokenStats()
        {
            return _stats;
        }

        public async Task ClearAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _cache.Clear();
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
                _lock.Release();
            }
        }

        public string HashText(string text)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash).Substring(0, 32);
        }

        public int CountTokens(string text)
        {
            return text?.Length / 4 ?? 0;
        }

        public long GetCacheSize()
        {
            return _cache.Count;
        }

        public Task<CachedPrompt> GetPromptAsync(string promptHash)
        {
            // Not used in demo
            return Task.FromResult<CachedPrompt>(null);
        }

        public Task CachePromptAsync(CachedPrompt prompt)
        {
            // Not used in demo
            return Task.CompletedTask;
        }

        public Task<CachedPrompt> GetOrCachePromptAsync(string promptText)
        {
            // Not used in demo
            return Task.FromResult<CachedPrompt>(null);
        }

        public Task<CachedCompletion> GetCompletionAsync(string completionHash)
        {
            // Not used in demo
            return Task.FromResult<CachedCompletion>(null);
        }

        public Task CacheCompletionAsync(CachedCompletion completion)
        {
            // Not used in demo
            return Task.CompletedTask;
        }

        public Task<CachedCompletion> GetOrCacheCompletionAsync(string promptText, string completionText, double confidence = 1.0)
        {
            // Not used in demo
            return Task.FromResult<CachedCompletion>(null);
        }

        public Task<int> GetCachedTokenCountAsync(string cacheKey)
        {
            // Not used in demo
            return Task.FromResult(0);
        }

        public Task CacheTokenCountAsync(string cacheKey, int tokenCount, TimeSpan? expiration = null)
        {
            // Not used in demo
            return Task.CompletedTask;
        }

        public Task<CachedEmbedding> GetEmbeddingAsync(string textHash)
        {
            // Not used in demo
            return Task.FromResult<CachedEmbedding>(null);
        }

        public Task CacheEmbeddingAsync(CachedEmbedding embedding)
        {
            // Not used in demo
            return Task.CompletedTask;
        }

        public Task<CachedEmbedding> GetOrCacheEmbeddingAsync(string text, Func<string, float[]> embeddingGenerator)
        {
            // Not used in demo
            return Task.FromResult<CachedEmbedding>(null);
        }

        private double CalculateHitRate()
        {
            var total = _stats.TotalPromptsCached + _stats.TotalCompletionsCached;
            return total > 0 ? 0.5 : 0; // Simple estimate for demo
        }
    }
}