using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    /// <summary>
    /// Main Context Compressor interface
    /// </summary>
    public interface IContextCompressor
    {
        Task<CompressionResult> CompressAsync(CompressableContext context, CancellationToken cancellationToken = default);
        bool ShouldCompress(CompressableContext context);
    }

    /// <summary>
    /// Token Estimator interface
    /// </summary>
    public interface ITokenEstimator
    {
        int EstimateTokens(string text);
        int EstimateTokens(object obj);
        int EstimateTokens(List<ChatMessage> messages);
        Dictionary<string, int> GetTokenDistribution(Dictionary<string, object> context);
    }

    /// <summary>
    /// Secret Redactor interface
    /// </summary>
    public interface ISecretRedactor
    {
        Task<CompressableContext> RedactAsync(CompressableContext context, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Tool Result Pruner interface
    /// </summary>
    public interface IToolResultPruner
    {
        Task<CompressableContext> PruneAsync(CompressableContext context, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Anchor Protection interface
    /// </summary>
    public interface IAnchorProtection
    {
        Task<List<Anchor>> IdentifyAnchorsAsync(CompressableContext context, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Progressive Compression interface
    /// </summary>
    public interface IProgressiveCompression
    {
        Task<(CompressableContext Context, List<CompressionStep> Steps)> CompressAsync(
            CompressableContext context,
            int targetTokens,
            List<Anchor> anchors,
            CancellationToken cancellationToken = default);
    }
}