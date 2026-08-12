// Core/Services/ProgressiveCompression.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartChatBot.ContextEngineering.Core.Interfaces;
using SmartChatBot.ContextEngineering.Core.Models;

namespace SmartChatBot.ContextEngineering.Core.Strategies
{
    /// <summary>
    /// Applies progressive compression tiers to reduce token count
    /// </summary>
    public class ProgressiveCompression : IProgressiveCompression
    {
        private readonly ITokenEstimator _tokenEstimator;
        private readonly ILogger<ProgressiveCompression> _logger;

        public ProgressiveCompression(
            ITokenEstimator tokenEstimator,
            ILogger<ProgressiveCompression> logger)
        {
            _tokenEstimator = tokenEstimator ?? throw new ArgumentNullException(nameof(tokenEstimator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(CompressableContext Context, List<CompressionStep> Steps)> CompressAsync(
            CompressableContext context,
            int targetTokens,
            List<Anchor> anchors,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var steps = new List<CompressionStep>();

            // Clone context
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

            var currentTokens = _tokenEstimator.EstimateTokens(compressed);

            _logger.LogDebug($"Starting compression. Current tokens: {currentTokens}, Target: {targetTokens}");

            if (currentTokens <= targetTokens)
            {
                _logger.LogDebug("Already within target, no compression needed");
                return (compressed, steps);
            }

            // TIER 1: Remove redundant messages
            if (compressed.Messages.Count > 5)
            {
                var saved = RemoveRedundantMessages(compressed, anchors);
                steps.Add(new CompressionStep
                {
                    Name = "RedundantMessageRemoval",
                    TokensSaved = saved,
                    Description = $"Removed {saved / 10} redundant messages"
                });
                _logger.LogDebug($"Tier 1: Removed redundant messages, saved {saved} tokens");
            }

            currentTokens = _tokenEstimator.EstimateTokens(compressed);
            if (currentTokens <= targetTokens)
            {
                _logger.LogDebug("Target reached after Tier 1");
                return (compressed, steps);
            }

            // TIER 2: Compact tool outputs
            if (compressed.ToolResults.Any())
            {
                var saved = CompactToolOutputs(compressed);
                steps.Add(new CompressionStep
                {
                    Name = "ToolOutputCompaction",
                    TokensSaved = saved,
                    Description = $"Compacted {compressed.ToolResults.Count} tool outputs"
                });
                _logger.LogDebug($"Tier 2: Compacted tool outputs, saved {saved} tokens");
            }

            currentTokens = _tokenEstimator.EstimateTokens(compressed);
            if (currentTokens <= targetTokens)
            {
                _logger.LogDebug("Target reached after Tier 2");
                return (compressed, steps);
            }

            // TIER 3: Summarize older messages
            if (compressed.Messages.Count > 3)
            {
                var saved = SummarizeOlderMessages(compressed, anchors);
                steps.Add(new CompressionStep
                {
                    Name = "MessageSummarization",
                    TokensSaved = saved,
                    Description = "Summarized older messages"
                });
                _logger.LogDebug($"Tier 3: Summarized older messages, saved {saved} tokens");
            }

            currentTokens = _tokenEstimator.EstimateTokens(compressed);
            if (currentTokens <= targetTokens)
            {
                _logger.LogDebug("Target reached after Tier 3");
                return (compressed, steps);
            }

            // TIER 4: Aggressive truncation (last resort)
            if (currentTokens > targetTokens)
            {
                var saved = AggressiveTruncation(compressed, targetTokens, anchors);
                steps.Add(new CompressionStep
                {
                    Name = "AggressiveTruncation",
                    TokensSaved = saved,
                    Description = "Emergency truncation applied"
                });
                _logger.LogWarning($"Tier 4: Aggressive truncation applied, saved {saved} tokens");
            }

            _logger.LogInformation(
                "Compression complete: {OriginalTokens} → {CompressedTokens} tokens ({Ratio:P2} reduction)",
                _tokenEstimator.EstimateTokens(context),
                _tokenEstimator.EstimateTokens(compressed),
                1 - ((double)_tokenEstimator.EstimateTokens(compressed) / _tokenEstimator.EstimateTokens(context)));

            return (compressed, steps);
        }

        private int RemoveRedundantMessages(CompressableContext context, List<Anchor> anchors)
        {
            var saved = 0;
            var protectedIndices = anchors
                .Where(a => a.Type == AnchorType.Message || a.Type == AnchorType.UserQuery)
                .Select(a => a.Index)
                .ToHashSet();

            var messages = context.Messages.ToList();
            var toRemove = messages
                .Select((msg, idx) => new { msg, idx })
                .Where(x => !protectedIndices.Contains(x.idx))
                .OrderBy(x => x.idx)
                .Take(Math.Max(0, messages.Count - 5))
                .ToList();

            foreach (var item in toRemove)
            {
                saved += _tokenEstimator.EstimateTokens(item.msg.Content);
                messages.Remove(item.msg);
            }

            context.Messages = messages;
            return saved;
        }

        private int CompactToolOutputs(CompressableContext context)
        {
            var saved = 0;
            var results = context.ToolResults.ToList();

            for (int i = 0; i < results.Count; i++)
            {
                var output = results[i].Output;
                if (output.Length > 1000)
                {
                    var truncated = output.Length > 2000
                        ? output[..2000] + "... (truncated)"
                        : output;

                    saved += _tokenEstimator.EstimateTokens(output) -
                            _tokenEstimator.EstimateTokens(truncated);

                    results[i] = new ToolResult
                    {
                        ToolName = results[i].ToolName,
                        Output = truncated,
                        IsError = results[i].IsError,
                        Timestamp = results[i].Timestamp
                    };
                }
            }

            context.ToolResults = results;
            return saved;
        }

        private int SummarizeOlderMessages(CompressableContext context, List<Anchor> anchors)
        {
            // This would use an LLM to summarize messages
            // For now, we just remove older messages
            var saved = 0;
            var protectedIndices = anchors
                .Where(a => a.Type == AnchorType.Message || a.Type == AnchorType.UserQuery)
                .Select(a => a.Index)
                .ToHashSet();

            var messages = context.Messages.ToList();
            var toKeep = messages
                .Select((msg, idx) => new { msg, idx })
                .Where(x => x.idx >= messages.Count - 3 || protectedIndices.Contains(x.idx))
                .Select(x => x.msg)
                .ToList();

            saved = (messages.Count - toKeep.Count) * 100;
            context.Messages = toKeep;

            return saved;
        }

        private int AggressiveTruncation(CompressableContext context, int targetTokens, List<Anchor> anchors)
        {
            var saved = 0;
            var protectedIndices = anchors
                .Where(a => a.Type == AnchorType.Message || a.Type == AnchorType.UserQuery)
                .Select(a => a.Index)
                .ToHashSet();

            var messages = context.Messages.ToList();
            var toKeep = messages
                .Select((msg, idx) => new { msg, idx })
                .Where(x => x.idx >= messages.Count - 2 || protectedIndices.Contains(x.idx))
                .Select(x => x.msg)
                .ToList();

            var removed = messages.Count - toKeep.Count;
            saved = removed * 100;

            context.Messages = toKeep;

            // Also truncate tool results if still over budget
            if (context.ToolResults.Count > 3)
            {
                context.ToolResults = context.ToolResults.Take(3).ToList();
                saved += 50 * (context.ToolResults.Count - 3);
            }

            return saved;
        }
    }
}