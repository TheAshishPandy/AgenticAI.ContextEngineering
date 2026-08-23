using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class TokenUsage
    {
        public int SystemPromptTokens { get; set; }
        public int SearchTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TokensGenerated { get; set; }
        public int TokensSaved { get; set; }
        public bool FromCache { get; set; }
        public double EstimatedCost => (SystemPromptTokens + SearchTokens + CompletionTokens + TokensGenerated) / 1000.0 * 0.02;
    }

    /// <summary>
    /// Optimized Request Options
    /// </summary>
    public class OptimizedRequestOptions
    {
        public string? SystemPrompt { get; set; }
        public int? MaxTokens { get; set; }
        public float? Temperature { get; set; }
        public bool? UseCache { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Optimized Response
    /// </summary>
    public class OptimizedResponse
    {
        public string Answer { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public bool FromCache { get; set; }
        public long ProcessingTimeMs { get; set; }
        public double Confidence { get; set; }
        public string? Error { get; set; }
        public TokenUsageDetail? TokenUsage { get; set; }
        public bool IsSuccess => string.IsNullOrEmpty(Error);
    }
}


