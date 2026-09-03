// AgenticAI.ContextEngineering.Core/Services/TokenOptimizedService.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly TokenOptimizedOptions _options;

        // ✅ Default values (will be overridden by appsettings.json)
        private const int DEFAULT_MAX_TOKENS = 150;
        private const float DEFAULT_TEMPERATURE = 0.2f;
        private const string DEFAULT_SYSTEM_PROMPT = "You are a helpful assistant. Answer concisely (1-3 sentences).";
        private const int DEFAULT_MAX_QUERY_LENGTH = 200;
        private const int DEFAULT_MAX_METADATA_LENGTH = 300;
        private const int DEFAULT_CACHE_EXPIRATION_HOURS = 24;

        public TokenOptimizedService(
            ITokenCache tokenCache,
            IAIResponseService aiResponseService,
            IOptions<TokenOptimizedOptions> options,
            ILogger<TokenOptimizedService> logger)
        {
            _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
            _aiResponseService = aiResponseService ?? throw new ArgumentNullException(nameof(aiResponseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new TokenOptimizedOptions();
        }

        public async Task<OptimizedResponse> GetOptimizedResponseAsync(
            string query,
            OptimizedRequestOptions? requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            requestOptions ??= new OptimizedRequestOptions();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // ✅ Use values from appsettings.json with fallback to defaults
                var systemPrompt = requestOptions.SystemPrompt ??
                                   _options.DefaultSystemPrompt ??
                                   DEFAULT_SYSTEM_PROMPT;

                var maxTokens = requestOptions.MaxTokens ??
                               _options.DefaultMaxTokens ??
                               DEFAULT_MAX_TOKENS;

                var temperature = requestOptions.Temperature ??
                                 _options.DefaultTemperature ??
                                 DEFAULT_TEMPERATURE;

                var maxQueryLength = _options.MaxQueryLength ?? DEFAULT_MAX_QUERY_LENGTH;
                var maxMetadataLength = _options.MaxMetadataLength ?? DEFAULT_MAX_METADATA_LENGTH;
                var cacheExpirationHours = _options.CacheExpirationHours ?? DEFAULT_CACHE_EXPIRATION_HOURS;

                // ✅ Truncate query if too long
                var truncatedQuery = TruncateString(query, maxQueryLength);
                if (truncatedQuery != query)
                {
                    _logger.LogWarning($"⚠️ Query truncated from {query.Length} to {truncatedQuery.Length} chars");
                }

                // ✅ Count tokens for the query
                var estimatedTokens = TokenCounter.CountTokens(truncatedQuery);
                _logger.LogInformation($"📝 Query token estimate: {estimatedTokens} tokens for: {truncatedQuery}");

                var cacheKey = $"optimized:{truncatedQuery.GetHashCode()}";

                if (requestOptions.UseCache == true)
                {
                    if (_tokenCache.TryGet<OptimizedResponse>(cacheKey, out var cachedResponse) && cachedResponse != null)
                    {
                        _logger.LogInformation($"✅ Cache HIT for: {truncatedQuery}");
                        cachedResponse.FromCache = true;
                        cachedResponse.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                        return cachedResponse;
                    }
                }

                _logger.LogInformation($"❌ Cache MISS for: {truncatedQuery}");

                // ✅ Count tokens for the full request
                var breakdown = TokenCounter.GetTokenBreakdown(
                    systemPrompt: systemPrompt,
                    query: truncatedQuery,
                    history: new List<ConversationMessage>(),
                    moduleData: requestOptions.Metadata?.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty),
                    maxTokens: maxTokens
                );

                TokenCounter.LogTokenBreakdown(breakdown, truncatedQuery);

                var request = new AIResponseRequest
                {
                    UserQuery = truncatedQuery,
                    Query = truncatedQuery,
                    SystemPrompt = systemPrompt,
                    MaxTokens = maxTokens,
                    Temperature = temperature,
                    UseCache = requestOptions.UseCache ?? true
                };

                // ✅ Truncate metadata values
                if (requestOptions.Metadata != null)
                {
                    foreach (var kvp in requestOptions.Metadata)
                    {
                        var value = kvp.Value?.ToString() ?? string.Empty;
                        request.ModuleData[kvp.Key] = TruncateString(value, maxMetadataLength);
                    }
                }

                var response = await _aiResponseService.GenerateResponseAsync(request, cancellationToken);

                if (!response.IsSuccess)
                {
                    return new OptimizedResponse
                    {
                        Query = truncatedQuery,
                        Error = response.Error,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        FromCache = false
                    };
                }

                var optimizedResponse = new OptimizedResponse
                {
                    Answer = response.Response,
                    Query = truncatedQuery,
                    FromCache = false,
                    ProcessingTimeMs = response.ProcessingTimeMs,
                    Confidence = response.Confidence > 0 ? response.Confidence : 0.75,
                    TokenUsage = new TokenUsageDetail
                    {
                        PromptTokens = response.PromptTokens,
                        CompletionTokens = response.CompletionTokens
                    }
                };

                // ✅ Log token savings
                var tokensSaved = breakdown.EstimatedTotalTokens - response.TokenCount;
                if (tokensSaved > 0)
                {
                    _logger.LogInformation(
                        "✅ Token savings: {TokensSaved} tokens saved! (Estimated: {Estimated}, Actual: {Actual})",
                        tokensSaved, breakdown.EstimatedTotalTokens, response.TokenCount);
                }

                // ✅ Log actual token usage
                _logger.LogInformation(
                    "📊 Actual token usage: Prompt={PromptTokens}, Completion={CompletionTokens}, Total={TotalTokens}",
                    response.PromptTokens, response.CompletionTokens, response.TokenCount);

                if (requestOptions.UseCache == true)
                {
                    _tokenCache.Set(
                        cacheKey,
                        optimizedResponse,
                        tokenCount: response.TokenCount,
                        expiration: TimeSpan.FromHours(cacheExpirationHours));
                    _logger.LogInformation($"✅ Response cached for: {truncatedQuery}");
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
            OptimizedRequestOptions? requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, OptimizedResponse>();

            var uniqueQueries = queries.Distinct().ToList();
            var lockObj = new object();

            await Task.Run(() =>
            {
                Parallel.ForEach(uniqueQueries, new ParallelOptions { MaxDegreeOfParallelism = 3 }, async (query) =>
                {
                    var response = await GetOptimizedResponseAsync(query, requestOptions, cancellationToken);
                    lock (lockObj)
                    {
                        results[query] = response;
                    }
                });
            });

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

        private string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            return value.Length > maxLength ? value[..maxLength] : value;
        }
    }

    /// <summary>
    /// Token Optimized Service Options
    /// </summary>
    public class TokenOptimizedOptions
    {
        public int? DefaultMaxTokens { get; set; }
        public float? DefaultTemperature { get; set; }
        public string? DefaultSystemPrompt { get; set; }
        public int? MaxQueryLength { get; set; }
        public int? MaxMetadataLength { get; set; }
        public int? CacheExpirationHours { get; set; }
        public bool? EnableQueryTruncation { get; set; }
        public bool? EnableMetadataTruncation { get; set; }
    }
}