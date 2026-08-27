// AgenticAI.ContextEngineering.Core/Services/TokenOptimizedService.cs
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

                var cacheKey = $"optimized:{query.GetHashCode()}";

                // Try to get from token cache
                if (options.UseCache == true)
                {
                    if (_tokenCache.TryGet<OptimizedResponse>(cacheKey, out var cachedResponse) && cachedResponse != null)
                    {
                        _logger.LogInformation($"✅ Cache HIT for: {query}");
                        cachedResponse.FromCache = true;
                        cachedResponse.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                        return cachedResponse;
                    }
                }

                _logger.LogInformation($"❌ Cache MISS for: {query}");

                var request = new AIResponseRequest
                {
                    UserQuery = query,
                    Query = query,
                    SystemPrompt = options.SystemPrompt ?? "You are a helpful assistant.",
                    MaxTokens = options.MaxTokens ?? 500,
                    Temperature = options.Temperature ?? 0.7f,
                    UseCache = options.UseCache ?? true
                };

                if (options.Metadata != null)
                {
                    foreach (var kvp in options.Metadata)
                    {
                        request.ModuleData[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                    }
                }

                var response = await _aiResponseService.GenerateResponseAsync(request, cancellationToken);

                if (!response.IsSuccess)
                {
                    return new OptimizedResponse
                    {
                        Query = query,
                        Error = response.Error,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        FromCache = false
                    };
                }

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
                        CompletionTokens = response.CompletionTokens
                    }
                };

                if (options.UseCache == true)
                {
                    _tokenCache.Set(
                        cacheKey,
                        optimizedResponse,
                        tokenCount: response.TokenCount,
                        expiration: TimeSpan.FromHours(24));
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
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    FromCache = false
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
            return _tokenCache.GetStats();
        }

        public async Task ClearTokenCacheAsync()
        {
            _tokenCache.Clear();
            _logger.LogInformation("🗑️ Token cache cleared");
            await Task.CompletedTask;
        }

        public async Task<string> GenerateTokenReportAsync()
        {
            return await _tokenCache.GenerateReportAsync();
        }
    }
}