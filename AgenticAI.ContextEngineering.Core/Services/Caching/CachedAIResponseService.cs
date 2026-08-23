// AgenticAI.ContextEngineering.Core/Services/Caching/CachedAIResponseService.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class CachedAIResponseService : IAIResponseService
    {
        private readonly IAIResponseService _inner;
        private readonly IKVCache _kvCache;
        private readonly ILogger<CachedAIResponseService> _logger;
        private const string CACHE_PREFIX = "ai:response:";

        public CachedAIResponseService(
            IAIResponseService inner,
            IKVCache kvCache,
            ILogger<CachedAIResponseService> logger)
        {
            _inner = inner;
            _kvCache = kvCache;
            _logger = logger;
        }

        public async Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // ✅ Generate consistent cache key from query and user
                var cacheKey = GenerateCacheKey(request);

                if (request.UseCache)
                {
                    // ✅ Try to get from KV Cache
                    var cached = await _kvCache.GetAsync<AIResponseResult>(cacheKey);
                    if (cached != null)
                    {
                        _logger.LogInformation($"✅ AI Response cache HIT: {request.Query}");
                        cached.FromCache = true;
                        cached.CacheLevel = "KVCache";
                        return cached;
                    }
                }

                _logger.LogInformation($"❌ AI Response cache MISS: {request.Query}");

                // ✅ Generate response
                var response = await _inner.GenerateResponseAsync(request, cancellationToken);

                // ✅ Cache the response if successful
                if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.Response) && request.UseCache)
                {
                    // ✅ Ensure response is properly marked as not from cache
                    response.FromCache = false;
                    response.CacheLevel = "Generated";

                    await _kvCache.SetAsync(cacheKey, response, TimeSpan.FromHours(24));
                    _logger.LogInformation($"✅ AI Response cached for: {request.Query} with key: {cacheKey}");
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CachedAIResponseService for: {Query}", request.Query);
                return new AIResponseResult
                {
                    Query = request.Query,
                    Error = ex.Message,
                    IsSuccess = false,
                    FromCache = false
                };
            }
        }

        public async IAsyncEnumerable<AIStreamChunk> GenerateStreamingResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            await foreach (var chunk in _inner.GenerateStreamingResponseAsync(request, cancellationToken))
            {
                yield return chunk;
            }
        }

        public TokenUsageStats GetTokenStats() => _inner.GetTokenStats();

        public async Task ClearTokenCacheAsync()
        {
            await _inner.ClearTokenCacheAsync();
            await _kvCache.ClearAsync();
            _logger.LogInformation("🗑️ All caches cleared");
        }

        public async Task<string> GenerateTokenReportAsync() => await _inner.GenerateTokenReportAsync();

        // ✅ Generate consistent cache key
        private string GenerateCacheKey(AIResponseRequest request)
        {
            // Use query, user ID, and a hash of the module data
            var queryKey = request.Query?.Trim().ToLowerInvariant() ?? string.Empty;
            var userId = request.UserId ?? "default";
            var key = $"{CACHE_PREFIX}{queryKey.GetHashCode()}_{userId}";

            // If there's module data, include it in the key
            if (request.ModuleData != null && request.ModuleData.Count > 0)
            {
                var moduleHash = string.Join("_", request.ModuleData.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
                key += $"_{moduleHash.GetHashCode()}";
            }

            return key;
        }
    }
}