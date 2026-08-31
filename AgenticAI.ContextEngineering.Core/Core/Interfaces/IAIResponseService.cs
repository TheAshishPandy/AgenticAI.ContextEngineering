using AgenticAI.ContextEngineering.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    /// <summary>
    /// AI Response Service Interface
    /// </summary>
    public interface IAIResponseService
    {
        /// <summary>
        /// Generate AI response for a request
        /// </summary>a
        Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate AI response with streaming support
        /// </summary>
        IAsyncEnumerable<AIStreamChunk> GenerateStreamingResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get token usage statistics
        /// </summary>
        TokenUsageStats GetTokenStats();

        /// <summary>
        /// Clear token cache
        /// </summary>
        Task ClearTokenCacheAsync();

        /// <summary>
        /// Generate token report
        /// </summary>
        Task<string> GenerateTokenReportAsync();
    }
}