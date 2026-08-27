// AgenticAI.ContextEngineering.Core/Models/Models.cs
using System;
using System.Collections.Generic;
using System.Threading;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class AIResponseRequest
    {
        public string ConversationId { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string UserQuery { get; set; } = string.Empty;
        public Dictionary<string, string> ModuleData { get; set; } = new();
        public string Query { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public List<ConversationMessage> ConversationHistory { get; set; } = new();
        public string ConversationSummary { get; set; } = string.Empty;
        public float Temperature { get; set; } = 0.2f;
        public int MaxTokens { get; set; } = 500;
        public bool RemoveUrlsFromResponse { get; set; } = true;
        public bool PrioritizeDynamicData { get; set; } = true;
        public List<string> DynamicDataKeywords { get; set; } = new()
        {
            "current", "latest", "recent", "today", "now", "new", "updated"
        };
        public bool UseCache { get; set; } = true;
        public bool Stream { get; set; } = false;
        public CancellationToken CancellationToken { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class ConversationMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class AIResponseResult
    {
        public string Response { get; set; } = string.Empty;
        public string OriginalResponse { get; set; } = string.Empty;
        public string ConversationSummary { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public TokenDetails? TokenDetails { get; set; }
        public bool UsedSummary { get; set; }
        public bool HadUrlsRemoved { get; set; }
        public string DynamicDataUsed { get; set; } = string.Empty;
        public string Answer
        {
            get => Response;
            set => Response = value;
        }
        public string Query { get; set; } = string.Empty;
        public bool FromCache { get; set; }
        public string CacheLevel { get; set; } = "None";
        public long CacheHitTimeMs { get; set; }
        public TokenUsageDetail TokenUsage { get; set; } = new();
        public List<SearchResult> Sources { get; set; } = new();
        public double Confidence { get; set; }
        public string Error { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public long ProcessingTimeMs { get; set; }
    }

    public class TokenDetails
    {
        public int TotalTokens { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int CachedTokens { get; set; }
        public int ReasoningTokens { get; set; }
        public int? AudioTokens { get; set; }
        public int? AcceptedPredictionTokens { get; set; }
        public int? RejectedPredictionTokens { get; set; }

        public override string ToString()
        {
            return $"Total: {TotalTokens}, Prompt: {PromptTokens}, Completion: {CompletionTokens}, " +
                   $"Cached: {CachedTokens}, Reasoning: {ReasoningTokens}";
        }
    }

    public class TokenUsageDetail
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens => PromptTokens + CompletionTokens;
        public int CachedTokens { get; set; }
        public int TokensSaved { get; set; }
        public double EstimatedCost => TotalTokens / 1000.0 * 0.02;
    }

    public class TokenUsageStats
    {
        public long TotalTokensCached { get; set; }
        public long TotalTokensSaved { get; set; }
        public long TotalPromptsCached { get; set; }
        public long TotalCompletionsCached { get; set; }
        public long TotalEmbeddingsCached { get; set; }
        public double CacheHitRate { get; set; }
        public double CostSaved { get; set; }
        public DateTime StatsUpdated { get; set; } = DateTime.UtcNow;
        public Dictionary<string, long> TokenSavingsByType { get; set; } = new();
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public decimal TotalCostSaved { get; set; }
        public int CacheEntryCount { get; set; }
    }

    public class AIStreamChunk
    {
        public string Content { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
        public string Error { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class SearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public double Score { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public ScoreBreakdown? ScoreBreakdown { get; set; }
    }

    public class ScoreBreakdown
    {
        public double LexicalScore { get; set; }
        public double SemanticScore { get; set; }
        public double CombinedScore { get; set; }
        public Dictionary<string, double>? TermScores { get; set; }
    }

    public class OptimizedResponse
    {
        public string Answer { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public bool FromCache { get; set; }
        public long ProcessingTimeMs { get; set; }
        public double Confidence { get; set; } = 0.75;
        public TokenUsageDetail? TokenUsage { get; set; }
    }

    public class OptimizedRequestOptions
    {
        public string? SystemPrompt { get; set; }
        public int? MaxTokens { get; set; }
        public float? Temperature { get; set; }
        public bool? UseCache { get; set; } = true;
        public Dictionary<string, object>? Metadata { get; set; }
    }
}