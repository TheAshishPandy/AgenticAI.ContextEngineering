// AgenticAI.ContextEngineering.Core/Services/TokenOptimizedService.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    /// <summary>
    /// Token Optimized Service - Uses IAIResponseService and ITokenCache
    /// </summary>
    public class TokenOptimizedService : ITokenOptimizedService
    {
        private readonly ITokenCache _tokenCache;
        private readonly IAIResponseService _aiResponseService;
        private readonly ILogger<TokenOptimizedService> _logger;

        public TokenOptimizedService(
            ITokenCache tokenCache,
            IAIResponseService aiResponseService,
            ILogger<TokenOptimizedService> logger)
        {
            _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
            _aiResponseService = aiResponseService ?? throw new ArgumentNullException(nameof(aiResponseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OptimizedResponse> GetOptimizedResponseAsync(
            string query,
            OptimizedRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new OptimizedRequestOptions();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"📝 Processing query: {query}");

                // ✅ Check cache first using ITokenCache
                var cacheKey = $"optimized:{query.GetHashCode()}";
                var cachedResponse = await _tokenCache.GetCachedResponseAsync<OptimizedResponse>(cacheKey);

                if (cachedResponse != null && options.UseCache == true)
                {
                    _logger.LogInformation($"✅ Cache HIT for: {query}");
                    cachedResponse.FromCache = true;
                    cachedResponse.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                    return cachedResponse;
                }

                _logger.LogInformation($"❌ Cache MISS for: {query}");

                // ✅ Build AI request
                var request = new AIResponseRequest
                {
                    Query = query,
                    UserQuery = query,
                    SystemPrompt = options.SystemPrompt ?? "You are a helpful assistant.",
                    MaxTokens = options.MaxTokens ?? 500,
                    Temperature = options.Temperature ?? 0.7f,
                    UseCache = options.UseCache ?? true,
                    ModuleData = options.Metadata ?? new Dictionary<string, string>()
                };

                // ✅ Generate AI response
                var response = await _aiResponseService.GenerateResponseAsync(request, cancellationToken);

                if (!response.IsSuccess)
                {
                    return new OptimizedResponse
                    {
                        Query = query,
                        Error = response.Error,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                // ✅ Track token usage
                await _tokenCache.TrackUsageAsync(
                    query,
                    response.PromptTokens,
                    response.CompletionTokens,
                    cancellationToken);

                // ✅ Create optimized response
                var optimizedResponse = new OptimizedResponse
                {
                    Answer = response.Response,
                    Query = query,
                    FromCache = false,
                    ProcessingTimeMs = response.ProcessingTimeMs,
                    Confidence = response.Confidence > 0 ? response.Confidence : 0.75,
                    TokenUsage = new TokenUsageDetail
                    {
                        PromptTokens = response.PromptTokens,
                        CompletionTokens = response.CompletionTokens,
                        CachedTokens = response.TokenCount
                    }
                };

                // ✅ Cache the response
                if (options.UseCache == true)
                {
                    await _tokenCache.CacheResponseAsync(cacheKey, optimizedResponse, TimeSpan.FromHours(24));
                    _logger.LogInformation($"✅ Response cached for: {query}");
                }

                return optimizedResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error for query: {query}");
                return new OptimizedResponse
                {
                    Query = query,
                    Error = ex.Message,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        public async Task<Dictionary<string, OptimizedResponse>> GetBatchOptimizedResponsesAsync(
            List<string> queries,
            OptimizedRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, OptimizedResponse>();

            foreach (var query in queries.Distinct())
            {
                var response = await GetOptimizedResponseAsync(query, options, cancellationToken);
                results[query] = response;
            }

            return results;
        }

        public TokenUsageStats GetTokenStats()
        {
            return _tokenCache.GetTokenStats();
        }

        public async Task ClearTokenCacheAsync()
        {
            await _tokenCache.ClearAsync();
            _logger.LogInformation("🗑️ Token cache cleared");
        }

        public async Task<string> GenerateTokenReportAsync()
        {
            var stats = _tokenCache.GetTokenStats();
            return $@"
╔══════════════════════════════════════════════════════════════╗
║           TOKEN OPTIMIZATION REPORT                         ║
╚══════════════════════════════════════════════════════════════╝
Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
Total Tokens Cached: {stats.TotalTokensCached:N0}
Total Tokens Saved: {stats.TotalTokensSaved:N0}
Cache Hit Rate: {stats.CacheHitRate:P2}
Cost Saved: ${stats.CostSaved:F2}
";
        }
    }
}