// Core/Services/ToolResultPruner.cs
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
    /// Prunes and compacts large tool outputs
    /// </summary>
    public class ToolResultPruner : IToolResultPruner
    {
        private readonly int _maxOutputLength;
        private readonly int _maxToolResults;

        public ToolResultPruner(int maxOutputLength = 2000, int maxToolResults = 10)
        {
            _maxOutputLength = maxOutputLength;
            _maxToolResults = maxToolResults;
        }

        public async Task<CompressableContext> PruneAsync(
            CompressableContext context,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var pruned = new CompressableContext
            {
                ConversationId = context.ConversationId,
                UserId = context.UserId,
                SystemPrompt = context.SystemPrompt,
                Messages = new List<ConversationMessage>(context.Messages),
                ToolResults = new List<ToolResult>(),
                Metadata = new Dictionary<string, object>(context.Metadata),
                Timestamp = context.Timestamp
            };

            if (context.ToolResults == null || !context.ToolResults.Any())
                return pruned;

            // Take only the most recent tool results
            var results = context.ToolResults
                .OrderByDescending(r => r.Timestamp)
                .Take(_maxToolResults)
                .ToList();

            foreach (var result in results)
            {
                var output = result.Output;

                // Prune large outputs
                if (output.Length > _maxOutputLength)
                {
                    var truncated = output.Length > 5000
                        ? output[..Math.Min(2000, output.Length)] + $"... (truncated, {output.Length} characters total)"
                        : output;

                    pruned.ToolResults.Add(new ToolResult
                    {
                        ToolName = result.ToolName,
                        Output = truncated,
                        IsError = result.IsError,
                        Timestamp = result.Timestamp
                    });
                }
                else
                {
                    pruned.ToolResults.Add(result);
                }
            }

            return pruned;
        }
    }
}