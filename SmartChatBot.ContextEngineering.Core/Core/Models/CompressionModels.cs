using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartChatBot.ContextEngineering.Core.Models
{
    /// <summary>
    /// Compressable context object
    /// </summary>
    public class CompressableContext
    {
        public string ConversationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public List<ChatMessage> Messages { get; set; } = new();
        public List<ToolResult> ToolResults { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public CompressableContext Clone()
        {
            return new CompressableContext
            {
                ConversationId = this.ConversationId,
                UserId = this.UserId,
                SystemPrompt = this.SystemPrompt,
                Messages = new List<ChatMessage>(this.Messages),
                ToolResults = new List<ToolResult>(this.ToolResults),
                Metadata = new Dictionary<string, object>(this.Metadata),
                Timestamp = this.Timestamp
            };
        }
    }

    /// <summary>
    /// Chat message
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Tool result
    /// </summary>
    public class ToolResult
    {
        public string ToolName { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public bool IsError { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Compression result
    /// </summary>
    public class CompressionResult
    {
        public CompressableContext OriginalContext { get; set; }
        public CompressableContext CompressedContext { get; set; }
        public int OriginalTokenCount { get; set; }
        public int CompressedTokenCount { get; set; }
        public double CompressionRatio { get; set; }
        public bool IsSuccess { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<CompressionStep> CompressionSteps { get; set; } = new();
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Compression step
    /// </summary>
    public class CompressionStep
    {
        public string Name { get; set; } = string.Empty;
        public int TokensSaved { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Compression options
    /// </summary>
    public class CompressionOptions
    {
        public int MaxTokens { get; set; } = 16000;
        public int? TargetTokenCount { get; set; }
        public bool EnableToolPruning { get; set; } = true;
        public bool EnableProgressiveCompression { get; set; } = true;
        public bool EnableAttentionOptimization { get; set; } = true;
        public bool EnableSecretRedaction { get; set; } = true;
        public int MaxToolOutputLength { get; set; } = 2000;
        public double CompressionThreshold { get; set; } = 0.85;
    }
}