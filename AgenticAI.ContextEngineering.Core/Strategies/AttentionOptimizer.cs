// Core/Services/AttentionOptimizer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Services
{
    /// <summary>
    /// Optimizes context placement for U-shaped attention patterns
    /// Models pay more attention to beginning and end of context
    /// </summary>
    public static class AttentionOptimizer
    {
        /// <summary>
        /// Reorder context for optimal model attention
        /// </summary>
        public static async Task<CompressableContext> OptimizeAsync(
            CompressableContext context,
            List<Anchor> anchors,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.Messages.Count <= 2)
                return context;

            var messages = context.Messages.ToList();
            var optimizedMessages = new List<ConversationMessage>();

            // Get important anchors for placement
            var topAnchors = anchors
                .Where(a => a.Type == AnchorType.Message || a.Type == AnchorType.UserQuery)
                .OrderByDescending(a => a.Importance)
                .Take(2)
                .Select(a => a.Index)
                .ToList();

            var bottomAnchors = anchors
                .Where(a => a.Type == AnchorType.Message || a.Type == AnchorType.UserQuery)
                .OrderByDescending(a => a.Importance)
                .Skip(2)
                .Take(2)
                .Select(a => a.Index)
                .ToList();

            var usedIndices = new HashSet<int>();

            // 1. Add top anchors FIRST (beginning of context - high attention)
            foreach (var idx in topAnchors)
            {
                if (idx >= 0 && idx < messages.Count && !usedIndices.Contains(idx))
                {
                    optimizedMessages.Add(messages[idx]);
                    usedIndices.Add(idx);
                }
            }

            // 2. Add middle messages (reduced attention)
            for (int i = 0; i < messages.Count; i++)
            {
                if (!usedIndices.Contains(i) && !bottomAnchors.Contains(i))
                {
                    optimizedMessages.Add(messages[i]);
                    usedIndices.Add(i);
                }
            }

            // 3. Add bottom anchors LAST (end of context - high attention)
            foreach (var idx in bottomAnchors)
            {
                if (idx >= 0 && idx < messages.Count && !usedIndices.Contains(idx))
                {
                    optimizedMessages.Add(messages[idx]);
                    usedIndices.Add(idx);
                }
            }

            // 4. Ensure user query is at the end (highest attention)
            var userQueryIdx = messages.Count - 1;
            if (userQueryIdx >= 0 && userQueryIdx < messages.Count)
            {
                var userQuery = messages[userQueryIdx];
                optimizedMessages.Remove(userQuery);
                optimizedMessages.Add(userQuery);
            }

            // Create new context with optimized messages
            return new CompressableContext
            {
                ConversationId = context.ConversationId,
                UserId = context.UserId,
                SystemPrompt = context.SystemPrompt,
                Messages = optimizedMessages,
                ToolResults = context.ToolResults,
                Metadata = context.Metadata,
                Timestamp = context.Timestamp
            };
        }

        /// <summary>
        /// Analyze attention distribution of current context
        /// </summary>
        public static AttentionAnalysis AnalyzeAttention(CompressableContext context)
        {
            if (context == null || context.Messages.Count == 0)
                return new AttentionAnalysis { IsOptimized = false };

            var analysis = new AttentionAnalysis
            {
                TotalMessages = context.Messages.Count,
                IsOptimized = true
            };

            // Check if important messages are at beginning/end
            var firstTwo = context.Messages.Take(2).ToList();
            var lastTwo = context.Messages.TakeLast(2).ToList();

            analysis.BeginningAttentionMessages = firstTwo.Count;
            analysis.EndAttentionMessages = lastTwo.Count;

            return analysis;
        }

        /// <summary>
        /// Get recommended placement for new messages
        /// </summary>
        public static PlacementRecommendation GetRecommendedPlacement(
            CompressableContext context,
            MessageImportance importance)
        {
            switch (importance)
            {
                case MessageImportance.Critical:
                    return new PlacementRecommendation
                    {
                        Position = "Beginning",
                        Reason = "Critical messages should be at the beginning for maximum attention",
                        Priority = 1
                    };
                case MessageImportance.High:
                    return new PlacementRecommendation
                    {
                        Position = "End",
                        Reason = "High importance messages should be near the end for recency effect",
                        Priority = 2
                    };
                case MessageImportance.Medium:
                    return new PlacementRecommendation
                    {
                        Position = "Middle",
                        Reason = "Medium importance messages can be placed in the middle",
                        Priority = 3
                    };
                default:
                    return new PlacementRecommendation
                    {
                        Position = "Middle",
                        Reason = "Low importance messages can be placed in the middle",
                        Priority = 4
                    };
            }
        }
    }

    public enum MessageImportance
    {
        Critical,
        High,
        Medium,
        Low
    }

    public class AttentionAnalysis
    {
        public int TotalMessages { get; set; }
        public bool IsOptimized { get; set; }
        public int BeginningAttentionMessages { get; set; }
        public int EndAttentionMessages { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public class PlacementRecommendation
    {
        public string Position { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public int Priority { get; set; }
    }
}