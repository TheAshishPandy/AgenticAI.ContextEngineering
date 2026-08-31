// AgenticAI.ContextEngineering.Core/Services/ITokenUsageTracker.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public interface ITokenUsageTracker
    {
        // Core methods
        Task<TokenUsageRecord> TrackCallAsync(string operationId, AIResponseResult response, AIResponseRequest request);
        Task<TokenUsageRecord> GetOperationAsync(string operationId);
        Task<IEnumerable<TokenUsageRecord>> GetRecentOperationsAsync(int count = 100);
        Task<TokenUsageSummary> GetSummaryAsync(DateTime? from = null, DateTime? to = null);
        Task ClearAsync();
        void UpdateCacheStatus(string operationId, bool fromCache, string cacheLevel);

        // Conversation-specific methods
        Task<ConversationTokenSummary> GetConversationSummaryAsync(string conversationId);
        Task<IEnumerable<TokenUsageRecord>> GetConversationCallsAsync(string conversationId, int count = 100);
        Task<Dictionary<string, ConversationTokenSummary>> GetAllConversationSummariesAsync();
        Task<ConversationTokenStats> GetConversationStatsAsync(string conversationId);
        Task<IEnumerable<string>> GetActiveConversationIdsAsync(TimeSpan? recentActivity = null);
        Task<ConversationUsageReport> GenerateConversationReportAsync(string conversationId);
        Task<AllConversationsReport> GenerateAllConversationsReportAsync();
    }

    // Core Models
    public class TokenUsageRecord
    {
        public string OperationId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Query { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens => PromptTokens + CompletionTokens;
        public int CachedTokens { get; set; }
        public int TokensSaved { get; set; }
        public decimal EstimatedCost { get; set; }
        public bool FromCache { get; set; }
        public string CacheLevel { get; set; }
        public long ResponseTimeMs { get; set; }
        public string ResponsePreview { get; set; }
        public string ConversationId { get; set; }
        public string Division { get; set; }
        public string UserId { get; set; }
        public double Confidence { get; set; }
        public bool IsSuccess { get; set; }
        public string Error { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class TokenUsageSummary
    {
        public int TotalCalls { get; set; }
        public int SuccessfulCalls { get; set; }
        public int FailedCalls { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public double HitRate { get; set; }
        public int TotalPromptTokens { get; set; }
        public int TotalCompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public int TotalCachedTokens { get; set; }
        public int TotalTokensSaved { get; set; }
        public decimal TotalEstimatedCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalCostSaved { get; set; }
        public Dictionary<string, int> TokensByModel { get; set; } = new();
        public Dictionary<string, int> TokensByDivision { get; set; } = new();
        public Dictionary<string, int> TokensByUser { get; set; } = new();
        public Dictionary<string, int> TokensByConversation { get; set; } = new();
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public long AverageResponseTimeMs { get; set; }
        public double AverageConfidence { get; set; }
        public List<TokenUsageRecord> RecentCalls { get; set; } = new();
        public Dictionary<string, double> CacheHitRateByDivision { get; set; } = new();
    }

    // Conversation Models
    public class ConversationTokenSummary
    {
        public string ConversationId { get; set; }
        public string Division { get; set; }
        public int TotalCalls { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public double HitRate { get; set; }
        public int TotalPromptTokens { get; set; }
        public int TotalCompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public int TotalCachedTokens { get; set; }
        public int TotalTokensSaved { get; set; }
        public decimal TotalEstimatedCost { get; set; }
        public decimal TotalCostSaved { get; set; }
        public DateTime FirstCall { get; set; }
        public DateTime LastCall { get; set; }
        public long AverageResponseTimeMs { get; set; }
        public double AverageConfidence { get; set; }
        public Dictionary<string, int> TokensByUser { get; set; } = new();
        public Dictionary<string, int> TokensByModel { get; set; } = new();
        public List<TokenUsageRecord> RecentCalls { get; set; } = new();
    }

    public class ConversationTokenStats
    {
        public string ConversationId { get; set; }
        public int TotalCalls { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public double HitRate { get; set; }
        public int TotalTokens { get; set; }
        public decimal TotalCost { get; set; }
        public decimal CostSaved { get; set; }
        public DateTime LastActivity { get; set; }
        public double AverageTokensPerCall { get; set; }
        public double AverageCostPerCall { get; set; }
        public Dictionary<string, int> TokensByModel { get; set; } = new();
        public Dictionary<string, int> TokensByUser { get; set; } = new();
    }

    public class ConversationUsageReport
    {
        public string ConversationId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public ConversationTokenSummary Summary { get; set; }
        public List<TokenUsageRecord> CallHistory { get; set; } = new();
        public Dictionary<string, object> Recommendations { get; set; } = new();
    }

    public class AllConversationsReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalConversations { get; set; }
        public int ActiveConversations { get; set; }
        public Dictionary<string, ConversationTokenSummary> Conversations { get; set; } = new();
        public ConversationTokenSummary OverallSummary { get; set; }
        public Dictionary<string, object> Recommendations { get; set; } = new();
    }
}