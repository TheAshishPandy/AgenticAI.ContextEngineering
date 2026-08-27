// AgenticAI.ContextEngineering.Core/Services/TokenAwareAIResponseService.cs
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
    public class TokenAwareAIResponseService : IAIResponseService
    {
        // ✅ Changed from IAIResponseService to AIResponseService (concrete type)
        private readonly AIResponseService _inner;
        private readonly ITokenCache _tokenCache;
        private readonly ILogger<TokenAwareAIResponseService> _logger;
        private const string CACHE_PREFIX = "token:ai:";

        // ✅ Constructor takes concrete AIResponseService
        public TokenAwareAIResponseService(
            AIResponseService inner,
            ITokenCache tokenCache,
            ILogger<TokenAwareAIResponseService> logger)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var cacheKey = GenerateCacheKey(request);
                _logger.LogDebug($"Token cache key: {cacheKey}");

                var cached = await _tokenCache.GetOrAddAsync<AIResponseResult>(
                    cacheKey,
                    async () =>
                    {
                        _logger.LogInformation($"Generating AI response for: {request.UserQuery}");
                        var response = await _inner.GenerateResponseAsync(request, cancellationToken);
                        if (response != null)
                        {
                            response.FromCache = false;
                            response.CacheLevel = "TokenCache";
                        }
                        return response;
                    },
                    tokenCount: 100,
                    costPerToken: 0.000001m
                );

                if (cached != null && cached.FromCache == false)
                {
                    cached.FromCache = true;
                    cached.CacheLevel = "TokenCache";
                    _logger.LogInformation($"✅ Token cache HIT: {request.UserQuery}");
                }
                else if (cached != null)
                {
                    _logger.LogInformation($"❌ Token cache MISS: {request.UserQuery}");
                }

                return cached ?? throw new InvalidOperationException("Response is null");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TokenAwareAIResponseService for: {Query}", request.UserQuery);
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
            return _tokenCache.GetStats();
        }

        public async Task ClearTokenCacheAsync()
        {
            _tokenCache.Clear();
            await _inner.ClearTokenCacheAsync();
            _logger.LogInformation("🗑️ Token cache cleared");
        }

        public async Task<string> GenerateTokenReportAsync()
        {
            return await _tokenCache.GenerateReportAsync();
        }

        private string GenerateCacheKey(AIResponseRequest request)
        {
            var queryKey = request.UserQuery?.Trim().ToLowerInvariant() ?? string.Empty;
            var userId = request.UserId ?? "default";
            var key = $"{CACHE_PREFIX}{queryKey.GetHashCode()}_{userId}";

            if (request.ModuleData != null && request.ModuleData.Count > 0)
            {
                var sortedData = request.ModuleData.OrderBy(k => k.Key);
                var moduleHash = string.Join("_", sortedData.Select(k => $"{k.Key}={k.Value}"));
                key += $"_{moduleHash.GetHashCode()}";
            }

            return key;
        }
    }
}