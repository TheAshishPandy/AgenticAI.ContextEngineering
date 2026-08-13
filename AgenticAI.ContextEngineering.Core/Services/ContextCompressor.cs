// Core/Services/ContextCompressor.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Strategies; 

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class ContextCompressor : IContextCompressor
    {
        private readonly ITokenEstimator _tokenEstimator;
        private readonly ISecretRedactor _secretRedactor;
        private readonly IToolResultPruner _toolPruner;
        private readonly IProgressiveCompression _progressiveCompression;
        private readonly IAnchorProtection _anchorProtection;
        private readonly ILogger<ContextCompressor> _logger;
        private readonly CompressionOptions _options;

        public ContextCompressor(
            ITokenEstimator tokenEstimator,
            ISecretRedactor secretRedactor,
            IToolResultPruner toolPruner,
            IProgressiveCompression progressiveCompression,
            IAnchorProtection anchorProtection,
            ILogger<ContextCompressor> logger,
            IOptions<CompressionOptions> options)
        {
            _tokenEstimator = tokenEstimator ?? throw new ArgumentNullException(nameof(tokenEstimator));
            _secretRedactor = secretRedactor ?? throw new ArgumentNullException(nameof(secretRedactor));
            _toolPruner = toolPruner ?? throw new ArgumentNullException(nameof(toolPruner));
            _progressiveCompression = progressiveCompression ?? throw new ArgumentNullException(nameof(progressiveCompression));
            _anchorProtection = anchorProtection ?? throw new ArgumentNullException(nameof(anchorProtection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new CompressionOptions();
        }

        public bool ShouldCompress(CompressableContext context)
        {
            var tokens = _tokenEstimator.EstimateTokens(context);
            return tokens > _options.MaxTokens * _options.CompressionThreshold;
        }

        public async Task<CompressionResult> CompressAsync(CompressableContext context,CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new CompressionResult
            {
                OriginalContext = context,
                OriginalTokenCount = _tokenEstimator.EstimateTokens(context),
                CompressionSteps = new List<CompressionStep>()
            };

            try
            {
                if (!ShouldCompress(context))
                {
                    result.CompressedContext = context;
                    result.CompressedTokenCount = result.OriginalTokenCount;
                    result.CompressionRatio = 0;
                    result.IsSuccess = true;
                    result.ProcessingTime = stopwatch.Elapsed;
                    return result;
                }

                // Manual clone
                var compressed = new CompressableContext
                {
                    ConversationId = context.ConversationId,
                    UserId = context.UserId,
                    SystemPrompt = context.SystemPrompt,
                    Messages = new List<ChatMessage>(context.Messages),
                    ToolResults = new List<ToolResult>(context.ToolResults),
                    Metadata = new Dictionary<string, object>(context.Metadata),
                    Timestamp = context.Timestamp
                };

                // STEP 1: Secret Redaction
                if (_options.EnableSecretRedaction)
                {
                    compressed = await _secretRedactor.RedactAsync(compressed, cancellationToken);
                }

                // STEP 2: Tool Result Pruning
                if (_options.EnableToolPruning)
                {
                    compressed = await _toolPruner.PruneAsync(compressed, cancellationToken);
                }

                // STEP 3: Identify Anchors
                var anchors = await _anchorProtection.IdentifyAnchorsAsync(compressed, cancellationToken);

                // STEP 4: Progressive Compression
                if (_options.EnableProgressiveCompression)
                {
                    var targetTokens = _options.TargetTokenCount ?? (int)(_options.MaxTokens * 0.8);
                    var (compressedContext, steps) = await _progressiveCompression.CompressAsync( compressed, targetTokens, anchors,cancellationToken);
                    compressed = compressedContext;
                    result.CompressionSteps.AddRange(steps);
                }
                // STEP 5: Attention Optimization
                if (_options.EnableAttentionOptimization)
                {
                    compressed = await AttentionOptimizer.OptimizeAsync(compressed, anchors, cancellationToken);
                }

                result.CompressedContext = compressed;
                result.CompressedTokenCount = _tokenEstimator.EstimateTokens(compressed);
                result.CompressionRatio = 1 - ((double)result.CompressedTokenCount / result.OriginalTokenCount);
                result.IsSuccess = true;
                result.ProcessingTime = stopwatch.Elapsed;
                _logger.LogInformation("Compression complete: {OriginalTokens} → {CompressedTokens} tokens ({Ratio:P2} reduction)", result.OriginalTokenCount, result.CompressedTokenCount,result.CompressionRatio);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Compression failed");
                result.IsSuccess = false;
                result.Error = ex.Message;
                result.ProcessingTime = stopwatch.Elapsed;
                return result;
            }
        }
    }
}