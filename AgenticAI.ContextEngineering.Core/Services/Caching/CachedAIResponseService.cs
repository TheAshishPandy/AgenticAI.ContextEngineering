// AgenticAI.ContextEngineering.Core/Services/Caching/CachedAIResponseService.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class CachedAIResponseService : IAIResponseService
    {
        // ✅ Changed from IAIResponseService to TokenAwareAIResponseService (concrete type)
        private readonly TokenAwareAIResponseService _inner;
        private readonly IKVCache _kvCache;
        private readonly ILogger<CachedAIResponseService> _logger;
        private const string CACHE_PREFIX = "ai:response:";

        // ✅ Constructor takes concrete TokenAwareAIResponseService
        public CachedAIResponseService(
            TokenAwareAIResponseService inner,
            IKVCache kvCache,
            ILogger<CachedAIResponseService> logger)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _kvCache = kvCache ?? throw new ArgumentNullException(nameof(kvCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var cacheKey = GenerateCacheKey(request);
                _logger.LogDebug($"KV cache key: {cacheKey}");

                if (request.UseCache)
                {
                    var cached = await _kvCache.GetAsync<AIResponseResult>(cacheKey);
                    if (cached != null)
                    {
                        _logger.LogInformation($"✅ KV Cache HIT: {request.UserQuery}");
                        cached.FromCache = true;
                        cached.CacheLevel = "KVCache";
                        return cached;
                    }
                }

                _logger.LogInformation($"❌ KV Cache MISS: {request.UserQuery}");

                var response = await _inner.GenerateResponseAsync(request, cancellationToken);

                    if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.Response) && request.UseCache)
                {
                    response.FromCache = false;
                    response.CacheLevel = "Generated";

                    await _kvCache.SetAsync(cacheKey, response, TimeSpan.FromHours(24));
                    _logger.LogInformation($"✅ AI Response cached in KV for: {request.UserQuery}");
                }

                return response ?? throw new InvalidOperationException("Response is null");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CachedAIResponseService for: {Query}", request.UserQuery);
                return new AIResponseResult
                {
                    Query = request.UserQuery,
                    Error = ex.Message,
                    IsSuccess = false,
                    FromCache = false
                };
            }
        }

        public async IAsyncEnumerable<AIStreamChunk> GenerateStreamingResponseAsync(
            AIResponseRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var chunk in _inner.GenerateStreamingResponseAsync(request, cancellationToken))
            {
                yield return chunk;
            }
        }

        public TokenUsageStats GetTokenStats()
        {
            return _inner.GetTokenStats();
        }

        public async Task ClearTokenCacheAsync()
        {
            await _inner.ClearTokenCacheAsync();
            await _kvCache.ClearAsync();
            _logger.LogInformation("🗑️ All caches cleared");
        }

        public async Task<string> GenerateTokenReportAsync()
        {
            return await _inner.GenerateTokenReportAsync();
        }

        private string GenerateCacheKey(AIResponseRequest request)
        {
            var queryKey = request.UserQuery?.Trim().ToLowerInvariant() ?? string.Empty;
            var userId = request.UserId ?? "default";
            var key = $"{CACHE_PREFIX}{queryKey.GetHashCode()}_{userId}";

            if (request.ModuleData != null && request.ModuleData.Count > 0)
            {
                var moduleHash = string.Join("_", request.ModuleData.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
                key += $"_{moduleHash.GetHashCode()}";
            }

            return key;
        }
    }
}