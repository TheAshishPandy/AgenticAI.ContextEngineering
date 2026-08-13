// Core/Services/AnchorProtection.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Strategies
{
    /// <summary>
    /// Identifies and protects critical context elements (anchors)
    /// </summary>
    public class AnchorProtection : IAnchorProtection
    {
        private readonly string[] _importantKeywords = new[]
        {
            "important", "critical", "essential", "key",
            "must", "required", "mandatory", "primary",
            "main", "core", "fundamental", "crucial",
            "urgent", "priority", "high priority", "blocker"
        };

        private readonly string[] _importantEntities = new[]
        {
            "user id", "userid", "account", "balance",
            "transaction", "order", "payment", "address",
            "phone", "email", "name", "date of birth"
        };

        public async Task<List<Anchor>> IdentifyAnchorsAsync(
            CompressableContext context,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var anchors = new List<Anchor>();

            // 1. System prompt - Always protected
            if (!string.IsNullOrEmpty(context.SystemPrompt))
            {
                anchors.Add(new Anchor
                {
                    Type = AnchorType.SystemPrompt,
                    Index = 0,
                    Reason = "System prompt contains critical instructions",
                    Importance = 1.0
                });
            }

            // 2. Current user query - Always protected
            if (context.Messages.Count > 0)
            {
                anchors.Add(new Anchor
                {
                    Type = AnchorType.UserQuery,
                    Index = context.Messages.Count - 1,
                    Reason = "Current user query must be preserved",
                    Importance = 1.0
                });
            }

            // 3. Recent messages - High importance
            for (int i = 0; i < context.Messages.Count; i++)
            {
                var msg = context.Messages[i];
                var importance = CalculateImportance(msg);

                if (importance > 0.5)
                {
                    anchors.Add(new Anchor
                    {
                        Type = AnchorType.Message,
                        Index = i,
                        Reason = importance > 0.8 ? "Critical message" : "Important message",
                        Importance = importance
                    });
                }
            }

            // 4. Important tool results
            for (int i = 0; i < context.ToolResults.Count; i++)
            {
                var result = context.ToolResults[i];
                var importance = CalculateImportance(result.Output);

                if (importance > 0.6)
                {
                    anchors.Add(new Anchor
                    {
                        Type = AnchorType.ToolResult,
                        Index = i,
                        Reason = "Contains important result data",
                        Importance = importance
                    });
                }
            }

            // Deduplicate and sort by importance
            return anchors
                .GroupBy(a => (a.Type, a.Index))
                .Select(g => g.OrderByDescending(a => a.Importance).First())
                .OrderByDescending(a => a.Importance)
                .ToList();
        }

        private double CalculateImportance(ConversationMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.Content))
                return 0;

            // Recent messages are more important
            // This would be calculated based on position

            var content = message.Content.ToLowerInvariant();

            // Check for important keywords
            foreach (var keyword in _importantKeywords)
            {
                if (content.Contains(keyword))
                    return 0.9;
            }

            foreach (var entity in _importantEntities)
            {
                if (content.Contains(entity))
                    return 0.8;
            }

            return 0.3;
        }

        private double CalculateImportance(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var content = text.ToLowerInvariant();

            foreach (var keyword in _importantKeywords)
            {
                if (content.Contains(keyword))
                    return 0.8;
            }

            foreach (var entity in _importantEntities)
            {
                if (content.Contains(entity))
                    return 0.7;
            }

            return 0.2;
        }
    }
}