// AgenticAI.ContextEngineering.Core/Services/TokenAwareAIResponseService.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class TokenAwareAIResponseService : IAIResponseService
    {
        private readonly IAIResponseService _innerService;
        private readonly ITokenCache _tokenCache;
        private readonly ITokenUsageTracker _tokenTracker;
        private readonly IKVCache _kvCache;
        private readonly ILogger<TokenAwareAIResponseService> _logger;
        private long _totalCalls = 0;
        private long _cacheHits = 0;
        private long _cacheMisses = 0;
        private long _totalPromptTokens = 0;
        private long _totalCompletionTokens = 0;
        private decimal _totalEstimatedCost = 0;
        private decimal _totalCostSaved = 0;

        public TokenAwareAIResponseService(
            IAIResponseService innerService,
            ITokenCache tokenCache,
            ITokenUsageTracker tokenTracker,
            IKVCache kvCache = null,
            ILogger<TokenAwareAIResponseService> logger = null)
        {
            _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
            _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
            _tokenTracker = tokenTracker ?? throw new ArgumentNullException(nameof(tokenTracker));
            _kvCache = kvCache;
            _logger = logger;
        }

        public async Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            var operationId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            bool fromCache = false;
            string cacheLevel = "Generated";

            try
            {
                // Check KV Cache first
                if (request.UseCache && _kvCache != null)
                {
                    var cacheKey = GenerateCacheKey(request);
                    var cachedResult = await _kvCache.GetAsync<AIResponseResult>(cacheKey);
                    if (cachedResult != null)
                    {
                        fromCache = true;
                        cacheLevel = "KVCache";
                        stopwatch.Stop();

                        cachedResult.FromCache = true;
                        cachedResult.CacheLevel = "KVCache";
                        cachedResult.CacheHitTimeMs = stopwatch.ElapsedMilliseconds;
                        cachedResult.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                        await TrackTokenUsageAsync(operationId, cachedResult, request);
                        _tokenTracker.UpdateCacheStatus(operationId, true, cacheLevel);

                        Interlocked.Increment(ref _totalCalls);
                        Interlocked.Increment(ref _cacheHits);
                        Interlocked.Add(ref _totalPromptTokens, cachedResult.PromptTokens);
                        Interlocked.Add(ref _totalCompletionTokens, cachedResult.CompletionTokens);
                        _totalEstimatedCost += (decimal)(cachedResult.TokenUsage?.EstimatedCost ?? 0);
                        _totalCostSaved += (decimal)(cachedResult.TokenUsage?.EstimatedCost ?? 0);

                        LogTokenUsage(operationId, cachedResult, stopwatch.ElapsedMilliseconds, true, request);
                        return cachedResult;
                    }
                }

                // Check Token Cache using ITokenCache methods
                if (request.UseCache)
                {
                    var tokenCacheKey = $"token:{request.UserQuery?.GetHashCode() ?? request.Query?.GetHashCode():x}";

                    // Try to get from token cache
                    if (_tokenCache.TryGet<AIResponseResult>(tokenCacheKey, out var cachedResponse) && cachedResponse != null)
                    {
                        fromCache = true;
                        cacheLevel = "TokenCache";
                        stopwatch.Stop();

                        cachedResponse.FromCache = true;
                        cachedResponse.CacheLevel = "TokenCache";
                        cachedResponse.CacheHitTimeMs = stopwatch.ElapsedMilliseconds;
                        cachedResponse.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                        await TrackTokenUsageAsync(operationId, cachedResponse, request);
                        _tokenTracker.UpdateCacheStatus(operationId, true, cacheLevel);

                        Interlocked.Increment(ref _totalCalls);
                        Interlocked.Increment(ref _cacheHits);
                        Interlocked.Add(ref _totalPromptTokens, cachedResponse.PromptTokens);
                        Interlocked.Add(ref _totalCompletionTokens, cachedResponse.CompletionTokens);
                        _totalEstimatedCost += (decimal)(cachedResponse.TokenUsage?.EstimatedCost ?? 0);
                        _totalCostSaved += (decimal)(cachedResponse.TokenUsage?.EstimatedCost ?? 0);

                        LogTokenUsage(operationId, cachedResponse, stopwatch.ElapsedMilliseconds, true, request);
                        return cachedResponse;
                    }
                }

                // Generate new response
                var response = await _innerService.GenerateResponseAsync(request, cancellationToken);
                stopwatch.Stop();

                response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                response.FromCache = false;
                response.CacheLevel = "Generated";
                response.Query = request.UserQuery ?? request.Query;

                await TrackTokenUsageAsync(operationId, response, request);

                // Cache the response
                if (request.UseCache && response.IsSuccess)
                {
                    if (_kvCache != null)
                    {
                        var cacheKey = GenerateCacheKey(request);
                        await _kvCache.SetAsync(cacheKey, response, TimeSpan.FromHours(1));
                    }

                    // Cache using ITokenCache
                    if (response.TokenCount > 0)
                    {
                        var tokenCacheKey = $"token:{request.UserQuery?.GetHashCode() ?? request.Query?.GetHashCode():x}";
                        _tokenCache.Set(tokenCacheKey, response, response.TokenCount, TimeSpan.FromHours(1));
                    }
                }

                Interlocked.Increment(ref _totalCalls);
                Interlocked.Increment(ref _cacheMisses);
                Interlocked.Add(ref _totalPromptTokens, response.PromptTokens);
                Interlocked.Add(ref _totalCompletionTokens, response.CompletionTokens);
                _totalEstimatedCost += (decimal)(response.TokenUsage?.EstimatedCost ?? 0);

                LogTokenUsage(operationId, response, stopwatch.ElapsedMilliseconds, false, request);
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(ex, "Error in token-aware response for operation {OperationId}", operationId);
                Console.WriteLine($"❌ Error in token-aware response: {ex.Message}");
                throw;
            }
        }

        public async IAsyncEnumerable<AIStreamChunk> GenerateStreamingResponseAsync(
            AIResponseRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var operationId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var fullResponse = new System.Text.StringBuilder();
            var promptTokens = 0;
            var completionTokens = 0;
            bool fromCache = false;
            string cacheLevel = "Generated";

            // Check if we have cached streaming response
            if (request.UseCache && _kvCache != null)
            {
                var cacheKey = GenerateCacheKey(request);
                var cachedResult = await _kvCache.GetAsync<AIResponseResult>(cacheKey);
                if (cachedResult != null && !string.IsNullOrEmpty(cachedResult.Response))
                {
                    fromCache = true;
                    cacheLevel = "KVCache";
                    stopwatch.Stop();

                    // Stream the cached response
                    var chunks = cachedResult.Response.Chunk(100);
                    foreach (var chunk in chunks)
                    {
                        yield return new AIStreamChunk
                        {
                            Content = chunk,
                            IsComplete = false,
                            Metadata = new Dictionary<string, object>
                            {
                                ["FromCache"] = true,
                                ["CacheLevel"] = "KVCache",
                                ["OperationId"] = operationId,
                                ["PromptTokens"] = cachedResult.PromptTokens,
                                ["CompletionTokens"] = cachedResult.CompletionTokens,
                                ["TokenCount"] = cachedResult.TokenCount
                            }
                        };
                        await Task.Delay(5, cancellationToken);
                    }

                    // Send completion chunk
                    yield return new AIStreamChunk
                    {
                        Content = string.Empty,
                        IsComplete = true,
                        Metadata = new Dictionary<string, object>
                        {
                            ["FromCache"] = true,
                            ["CacheLevel"] = "KVCache",
                            ["OperationId"] = operationId,
                            ["ProcessingTimeMs"] = stopwatch.ElapsedMilliseconds,
                            ["TokenCount"] = cachedResult.TokenCount,
                            ["PromptTokens"] = cachedResult.PromptTokens,
                            ["CompletionTokens"] = cachedResult.CompletionTokens
                        }
                    };

                    Interlocked.Increment(ref _totalCalls);
                    Interlocked.Increment(ref _cacheHits);
                    Interlocked.Add(ref _totalPromptTokens, cachedResult.PromptTokens);
                    Interlocked.Add(ref _totalCompletionTokens, cachedResult.CompletionTokens);
                    _totalCostSaved += (decimal)(cachedResult.TokenUsage?.EstimatedCost ?? 0);

                    var cachedResponse = new AIResponseResult
                    {
                        Response = cachedResult.Response,
                        Query = request.UserQuery ?? request.Query,
                        FromCache = true,
                        CacheLevel = "KVCache",
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        TokenCount = cachedResult.TokenCount,
                        PromptTokens = cachedResult.PromptTokens,
                        CompletionTokens = cachedResult.CompletionTokens,
                        IsSuccess = true,
                        TokenUsage = cachedResult.TokenUsage
                    };

                    await TrackTokenUsageAsync(operationId, cachedResponse, request);
                    LogTokenUsage(operationId, cachedResponse, stopwatch.ElapsedMilliseconds, true, request);
                    yield break;
                }
            }

            // Stream from inner service
            await foreach (var chunk in _innerService.GenerateStreamingResponseAsync(request, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    fullResponse.Append(chunk.Content);
                }

                // Track token counts from metadata if available
                if (chunk.Metadata != null)
                {
                    if (chunk.Metadata.TryGetValue("PromptTokens", out var pt) && pt is int p)
                        promptTokens = p;
                    if (chunk.Metadata.TryGetValue("CompletionTokens", out var ct) && ct is int c)
                        completionTokens = c;
                }

                yield return chunk;
            }

            stopwatch.Stop();

            // Track the completed streaming response
            if (fullResponse.Length > 0)
            {
                var response = new AIResponseResult
                {
                    Response = fullResponse.ToString(),
                    Query = request.UserQuery ?? request.Query,
                    FromCache = false,
                    CacheLevel = "Generated",
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    TokenCount = promptTokens + completionTokens,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    IsSuccess = true,
                    Confidence = 0.95,
                    TokenUsage = new TokenUsageDetail
                    {
                        PromptTokens = promptTokens,
                        CompletionTokens = completionTokens,
                        TokensSaved = 0
                    }
                };

                await TrackTokenUsageAsync(operationId, response, request);
                Interlocked.Increment(ref _totalCalls);
                Interlocked.Increment(ref _cacheMisses);
                Interlocked.Add(ref _totalPromptTokens, promptTokens);
                Interlocked.Add(ref _totalCompletionTokens, completionTokens);
                _totalEstimatedCost += (decimal)(response.TokenUsage?.EstimatedCost ?? 0);

                LogTokenUsage(operationId, response, stopwatch.ElapsedMilliseconds, false, request);
            }
        }

        // Update the GetTokenStats method in TokenAwareAIResponseService
        public TokenUsageStats GetTokenStats()
        {
            var cacheStats = _tokenCache.GetStats();

            var stats = new TokenUsageStats
            {
                TotalTokensCached = cacheStats?.TotalTokensCached ?? 0,
                TotalTokensSaved = cacheStats?.TotalTokensSaved ?? 0,
                TotalPromptsCached = _totalPromptTokens,
                TotalCompletionsCached = _totalCompletionTokens,
                TotalEmbeddingsCached = 0,
                CacheHitRate = _totalCalls > 0 ? (double)_cacheHits / _totalCalls * 100 : 0,
                CostSaved = (double)_totalCostSaved,
                StatsUpdated = DateTime.UtcNow,
                TokenSavingsByType = new Dictionary<string, long>
                {
                    ["Prompt"] = _totalPromptTokens,
                    ["Completion"] = _totalCompletionTokens,
                    ["Cached"] = cacheStats?.TotalTokensCached ?? 0
                },
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                TotalCostSaved = _totalCostSaved,
                CacheEntryCount = cacheStats?.CacheEntryCount ?? 0
            };

            return stats;
        }

        public async Task<string> GenerateTokenReportAsync()
        {
            try
            {
                var summary = await _tokenTracker.GetSummaryAsync();
                var stats = GetTokenStats();
                var cacheReport = await _tokenCache.GenerateReportAsync();

                var report = $@"
═══════════════════════════════════════════════════════════════════
📊 TOKEN USAGE REPORT
═══════════════════════════════════════════════════════════════════
  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

  📈 SUMMARY STATISTICS:
     Total Calls:        {_totalCalls,12:N0}
     Cache Hits:         {_cacheHits,12:N0} ({stats.CacheHitRate:F1}%)
     Cache Misses:       {_cacheMisses,12:N0}
     
  🔢 TOKEN USAGE:
     Total Prompt Tokens:    {_totalPromptTokens,12:N0}
     Total Completion Tokens:{_totalCompletionTokens,12:N0}
     Total Tokens:           {_totalPromptTokens + _totalCompletionTokens,12:N0}
     
  💰 COST:
     Estimated Cost:     ${_totalEstimatedCost,12:F6}
     Cost Saved:         ${_totalCostSaved,12:F6}

  ⚡ CACHE STATISTICS:
     Cache Entries:      {stats.CacheEntryCount,12:N0}
     Cache Hit Rate:     {stats.CacheHitRate,12:F1}%
     Tokens Cached:      {stats.TotalTokensCached,12:N0}
     Tokens Saved:       {stats.TotalTokensSaved,12:N0}

  📄 CACHE REPORT:
{cacheReport}
";

                if (summary != null && summary.RecentCalls != null && summary.RecentCalls.Any())
                {
                    report += $@"
  🔄 RECENT CALLS (Last {Math.Min(5, summary.RecentCalls.Count)}):
";
                    foreach (var call in summary.RecentCalls.Take(5))
                    {
                        var queryPreview = call.Query?.Length > 40 ? call.Query[..40] + "..." : call.Query;
                        report += $"     {call.Timestamp.ToLocalTime():HH:mm:ss} | " +
                                 $"{(call.FromCache ? "✅" : "❌")} | " +
                                 $"{call.TotalTokens,6} tokens | " +
                                 $"\"{queryPreview}\"\n";
                    }
                }

                report += @"
═══════════════════════════════════════════════════════════════════
";

                return report;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating token report");
                return $"Error generating report: {ex.Message}";
            }
        }

        public async Task ClearTokenCacheAsync()
        {
            try
            {
                // Clear token cache using ITokenCache.Clear()
                _tokenCache.Clear();
                await _tokenTracker.ClearAsync();

                _totalCalls = 0;
                _cacheHits = 0;
                _cacheMisses = 0;
                _totalPromptTokens = 0;
                _totalCompletionTokens = 0;
                _totalEstimatedCost = 0;
                _totalCostSaved = 0;

                Console.WriteLine("✅ Token cache cleared successfully");
                _logger?.LogInformation("Token cache cleared successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing token cache");
                Console.WriteLine($"❌ Error clearing token cache: {ex.Message}");
                throw;
            }
        }

        private async Task TrackTokenUsageAsync(string operationId, AIResponseResult response, AIResponseRequest request)
        {
            await _tokenTracker.TrackCallAsync(operationId, response, request);
        }

        private void LogTokenUsage(string operationId, AIResponseResult response, long responseTimeMs, bool fromCache, AIResponseRequest request)
        {
            var logMessage = $@"
🔢 TOKEN USAGE [{operationId[..Math.Min(8, operationId.Length)]}]
   Query: {response.Query?[..Math.Min(60, response.Query?.Length ?? 0)]}...
   Prompt Tokens: {response.PromptTokens:N0}
   Completion Tokens: {response.CompletionTokens:N0}
   Total Tokens: {response.TokenCount:N0}
   Estimated Cost: ${response.TokenUsage?.EstimatedCost ?? 0:F6}
   Response Time: {responseTimeMs}ms
   From Cache: {(fromCache ? "✅" : "❌")}
   Cache Level: {response.CacheLevel ?? "Generated"}
   Confidence: {response.Confidence:F1}%
   Division: {request?.Division ?? "N/A"}
   {new string('─', 50)}";

            Console.WriteLine(logMessage);
            _logger?.LogInformation(logMessage);
        }

        private string GenerateCacheKey(AIResponseRequest request)
        {
            var query = request.UserQuery ?? request.Query;
            var division = request.Division ?? "default";
            return $"response:{division}:{query?.GetHashCode():x}";
        }
    }

    // Extension method for string chunking
    public static class StringExtensions
    {
        public static IEnumerable<string> Chunk(this string str, int chunkSize)
        {
            for (int i = 0; i < str.Length; i += chunkSize)
            {
                yield return str.Substring(i, Math.Min(chunkSize, str.Length - i));
            }
        }
    }
}