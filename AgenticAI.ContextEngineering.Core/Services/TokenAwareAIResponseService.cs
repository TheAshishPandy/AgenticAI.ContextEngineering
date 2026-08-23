// AgenticAI.ContextEngineering.Core/Services/TokenAwareAIResponseService.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class TokenAwareAIResponseService : IAIResponseService
    {
        private readonly IAIResponseService _inner;
        private readonly ITokenCache _tokenCache;
        private readonly ILogger<TokenAwareAIResponseService> _logger;

        public TokenAwareAIResponseService(
            IAIResponseService inner,
            ITokenCache tokenCache,
            ILogger<TokenAwareAIResponseService> logger)
        {
            _inner = inner;
            _tokenCache = tokenCache;
            _logger = logger;
        }

        public async Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = await _inner.GenerateResponseAsync(request, cancellationToken);

            if (result != null && result.IsSuccess)
            {
                await _tokenCache.TrackUsageAsync(
                    request.Query,
                    result.PromptTokens,
                    result.CompletionTokens,
                    cancellationToken);
            }

            return result;
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

        public TokenUsageStats GetTokenStats() => _tokenCache.GetTokenStats();
        public async Task ClearTokenCacheAsync() => await _tokenCache.ClearAsync();
        public async Task<string> GenerateTokenReportAsync()
        {
            var stats = GetTokenStats();
            return await Task.FromResult($@"
Total Tokens Saved: {stats.TotalTokensSaved:N0}
Cache Hit Rate: {stats.CacheHitRate:P2}
Cost Saved: ${stats.CostSaved:F2}
");
        }
    }
}