// AgenticAI.ContextEngineering.Core/Services/CachedAIResponseService.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
                if (request.UseCache)
                {
                    var cacheKey = $"{CACHE_PREFIX}{request.Query.GetHashCode()}_{request.UserId}";
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
                var response = await _inner.GenerateResponseAsync(request, cancellationToken);

                if (response != null && response.IsSuccess && request.UseCache)
                {
                    var cacheKey = $"{CACHE_PREFIX}{request.Query.GetHashCode()}_{request.UserId}";
                    await _kvCache.SetAsync(cacheKey, response, TimeSpan.FromHours(24));
                    _logger.LogInformation($"✅ AI Response cached: {request.Query}");
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
                    IsSuccess = false
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
        public async Task ClearTokenCacheAsync() => await _inner.ClearTokenCacheAsync();
        public async Task<string> GenerateTokenReportAsync() => await _inner.GenerateTokenReportAsync();
    }
}