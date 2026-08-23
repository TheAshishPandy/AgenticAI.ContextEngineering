// AgenticAI.ContextEngineering.Core/Interfaces/ITokenOptimizedService.cs
using AgenticAI.ContextEngineering.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    /// <summary>
    /// Token Optimized Service Interface
    /// </summary>
    public interface ITokenOptimizedService
    {
        /// <summary>
        /// Get optimized response for a single query
        /// </summary>
        Task<OptimizedResponse> GetOptimizedResponseAsync(
            string query,
            OptimizedRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get optimized responses for multiple queries in batch
        /// </summary>
        Task<Dictionary<string, OptimizedResponse>> GetBatchOptimizedResponsesAsync(
            List<string> queries,
            OptimizedRequestOptions? options = null,
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