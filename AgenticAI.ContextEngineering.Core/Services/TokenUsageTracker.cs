// AgenticAI.ContextEngineering.Core/Services/TokenUsageTracker.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class TokenUsageTracker : ITokenUsageTracker
    {
        private readonly ConcurrentDictionary<string, TokenUsageRecord> _records = new();
        private readonly ConcurrentQueue<string> _operationOrder = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _conversationCalls = new();
        private readonly ConcurrentDictionary<string, DateTime> _conversationLastActivity = new();
        private readonly ILogger<TokenUsageTracker> _logger;
        private readonly int _maxRecords = 10000;
        private readonly object _lock = new();

        public TokenUsageTracker(ILogger<TokenUsageTracker> logger = null)
        {
            _logger = logger;
        }

        public Task<TokenUsageRecord> TrackCallAsync(string operationId, AIResponseResult response, AIResponseRequest request)
        {
            try
            {
                var conversationId = request.ConversationId ?? "unknown";

                var record = new TokenUsageRecord
                {
                    OperationId = operationId,
                    Timestamp = DateTime.UtcNow,
                    Query = request.UserQuery ?? request.Query,
                    PromptTokens = response.PromptTokens,
                    CompletionTokens = response.CompletionTokens,
                    CachedTokens = response.TokenDetails?.CachedTokens ?? 0,
                    TokensSaved = response.TokenUsage?.TokensSaved ?? 0,
                    EstimatedCost = (decimal)(response.TokenUsage?.EstimatedCost ?? 0),
                    FromCache = response.FromCache,
                    CacheLevel = response.CacheLevel ?? "Generated",
                    ResponseTimeMs = response.ProcessingTimeMs,
                    ResponsePreview = response.Response?.Length > 100 ? response.Response[..100] + "..." : response.Response,
                    ConversationId = conversationId,
                    Division = request.Division,
                    UserId = request.UserId,
                    Confidence = response.Confidence,
                    IsSuccess = response.IsSuccess,
                    Error = response.Error,
                    Metadata = response.Metadata ?? new Dictionary<string, object>()
                };

                lock (_lock)
                {
                    _records[operationId] = record;
                    _operationOrder.Enqueue(operationId);

                    if (!_conversationCalls.ContainsKey(conversationId))
                    {
                        _conversationCalls[conversationId] = new HashSet<string>();
                    }
                    _conversationCalls[conversationId].Add(operationId);
                    _conversationLastActivity[conversationId] = DateTime.UtcNow;

                    while (_operationOrder.Count > _maxRecords && _operationOrder.TryDequeue(out var oldId))
                    {
                        if (_records.TryRemove(oldId, out var removedRecord))
                        {
                            if (!string.IsNullOrEmpty(removedRecord.ConversationId) &&
                                _conversationCalls.TryGetValue(removedRecord.ConversationId, out var callIds))
                            {
                                callIds.Remove(oldId);
                                if (callIds.Count == 0)
                                {
                                    _conversationCalls.TryRemove(removedRecord.ConversationId, out _);
                                    _conversationLastActivity.TryRemove(removedRecord.ConversationId, out _);
                                }
                            }
                        }
                    }
                }

                return Task.FromResult(record);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error tracking token usage for operation {OperationId}", operationId);
                throw;
            }
        }

        public void UpdateCacheStatus(string operationId, bool fromCache, string cacheLevel)
        {
            if (_records.TryGetValue(operationId, out var record))
            {
                record.FromCache = fromCache;
                record.CacheLevel = cacheLevel;
            }
        }

        public Task<TokenUsageRecord> GetOperationAsync(string operationId)
        {
            _records.TryGetValue(operationId, out var record);
            return Task.FromResult(record);
        }

        public Task<IEnumerable<TokenUsageRecord>> GetRecentOperationsAsync(int count = 100)
        {
            var recent = _operationOrder
                .Reverse()
                .Take(count)
                .Select(id => _records.TryGetValue(id, out var record) ? record : null)
                .Where(r => r != null)
                .Cast<TokenUsageRecord>()
                .ToList();

            return Task.FromResult<IEnumerable<TokenUsageRecord>>(recent);
        }

        public Task<IEnumerable<TokenUsageRecord>> GetConversationCallsAsync(string conversationId, int count = 100)
        {
            if (string.IsNullOrEmpty(conversationId))
                return Task.FromResult(Enumerable.Empty<TokenUsageRecord>());

            if (_conversationCalls.TryGetValue(conversationId, out var callIds))
            {
                var calls = callIds
                    .Select(id => _records.TryGetValue(id, out var record) ? record : null)
                    .Where(r => r != null)
                    .Cast<TokenUsageRecord>()
                    .OrderByDescending(r => r.Timestamp)
                    .Take(count)
                    .ToList();

                return Task.FromResult<IEnumerable<TokenUsageRecord>>(calls);
            }

            return Task.FromResult(Enumerable.Empty<TokenUsageRecord>());
        }

        public Task<ConversationTokenSummary> GetConversationSummaryAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
                return Task.FromResult(new ConversationTokenSummary { ConversationId = conversationId });

            var calls = GetConversationCallsAsync(conversationId).Result?.ToList() ?? new List<TokenUsageRecord>();

            if (!calls.Any())
            {
                return Task.FromResult(new ConversationTokenSummary
                {
                    ConversationId = conversationId,
                    TotalCalls = 0
                });
            }

            var summary = new ConversationTokenSummary
            {
                ConversationId = conversationId,
                Division = calls.FirstOrDefault()?.Division ?? "Unknown",
                TotalCalls = calls.Count,
                CacheHits = calls.Count(r => r.FromCache),
                CacheMisses = calls.Count(r => !r.FromCache),
                TotalPromptTokens = calls.Sum(r => r.PromptTokens),
                TotalCompletionTokens = calls.Sum(r => r.CompletionTokens),
                TotalTokens = calls.Sum(r => r.TotalTokens),
                TotalCachedTokens = calls.Sum(r => r.CachedTokens),
                TotalTokensSaved = calls.Sum(r => r.TokensSaved),
                TotalEstimatedCost = calls.Sum(r => r.EstimatedCost),
                TotalCostSaved = calls.Sum(r => r.EstimatedCost),
                FirstCall = calls.Min(r => r.Timestamp),
                LastCall = calls.Max(r => r.Timestamp),
                AverageResponseTimeMs = (long)calls.Average(r => r.ResponseTimeMs),
                AverageConfidence = calls.Average(r => r.Confidence),
                TokensByUser = calls
                    .Where(r => !string.IsNullOrEmpty(r.UserId))
                    .GroupBy(r => r.UserId)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens)),
                TokensByModel = calls
                    .GroupBy(r => "gpt-4")
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens)),
                RecentCalls = calls.OrderByDescending(r => r.Timestamp).Take(10).ToList()
            };

            summary.HitRate = summary.TotalCalls > 0 ? (double)summary.CacheHits / summary.TotalCalls * 100 : 0;

            return Task.FromResult(summary);
        }

        public Task<Dictionary<string, ConversationTokenSummary>> GetAllConversationSummariesAsync()
        {
            var summaries = new Dictionary<string, ConversationTokenSummary>();

            foreach (var conversationId in _conversationCalls.Keys)
            {
                var summary = GetConversationSummaryAsync(conversationId).Result;
                if (summary != null && summary.TotalCalls > 0)
                {
                    summaries[conversationId] = summary;
                }
            }

            return Task.FromResult(summaries);
        }

        public Task<ConversationTokenStats> GetConversationStatsAsync(string conversationId)
        {
            var summary = GetConversationSummaryAsync(conversationId).Result;

            if (summary == null || summary.TotalCalls == 0)
            {
                return Task.FromResult(new ConversationTokenStats
                {
                    ConversationId = conversationId,
                    TotalCalls = 0
                });
            }

            var stats = new ConversationTokenStats
            {
                ConversationId = conversationId,
                TotalCalls = summary.TotalCalls,
                CacheHits = summary.CacheHits,
                CacheMisses = summary.CacheMisses,
                HitRate = summary.HitRate,
                TotalTokens = summary.TotalTokens,
                TotalCost = summary.TotalEstimatedCost,
                CostSaved = summary.TotalCostSaved,
                LastActivity = summary.LastCall,
                AverageTokensPerCall = summary.TotalCalls > 0 ? (double)summary.TotalTokens / summary.TotalCalls : 0,
                AverageCostPerCall = summary.TotalCalls > 0 ? (double)summary.TotalEstimatedCost / summary.TotalCalls : 0,
                TokensByModel = summary.TokensByModel,
                TokensByUser = summary.TokensByUser
            };

            return Task.FromResult(stats);
        }

        public Task<IEnumerable<string>> GetActiveConversationIdsAsync(TimeSpan? recentActivity = null)
        {
            var threshold = recentActivity ?? TimeSpan.FromHours(24);
            var cutoff = DateTime.UtcNow.Subtract(threshold);

            var activeIds = _conversationLastActivity
                .Where(kvp => kvp.Value >= cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            return Task.FromResult<IEnumerable<string>>(activeIds);
        }

        public Task<ConversationUsageReport> GenerateConversationReportAsync(string conversationId)
        {
            var summary = GetConversationSummaryAsync(conversationId).Result;
            var calls = GetConversationCallsAsync(conversationId, 20).Result?.ToList() ?? new List<TokenUsageRecord>();

            var report = new ConversationUsageReport
            {
                ConversationId = conversationId,
                GeneratedAt = DateTime.UtcNow,
                Summary = summary,
                CallHistory = calls,
                Recommendations = new Dictionary<string, object>
                {
                    ["Cache Efficiency"] = summary?.HitRate >= 80 ? "Excellent" : summary?.HitRate >= 60 ? "Good" : "Consider improving cache strategy",
                    ["Token Usage"] = summary?.TotalTokens > 10000 ? "High token usage - consider optimization" : "Normal token usage",
                    ["Average Tokens Per Call"] = summary?.TotalCalls > 0 ? summary.TotalTokens / summary.TotalCalls : 0,
                    ["Cost Optimization"] = summary?.TotalCostSaved > 0 ? $"Saved ${summary.TotalCostSaved:F4}" : "No cost savings from cache"
                }
            };

            return Task.FromResult(report);
        }

        public Task<AllConversationsReport> GenerateAllConversationsReportAsync()
        {
            var allSummaries = GetAllConversationSummariesAsync().Result ?? new Dictionary<string, ConversationTokenSummary>();
            var activeIds = GetActiveConversationIdsAsync().Result?.ToList() ?? new List<string>();

            var overallSummary = new ConversationTokenSummary
            {
                ConversationId = "Overall",
                TotalCalls = allSummaries.Values.Sum(s => s.TotalCalls),
                CacheHits = allSummaries.Values.Sum(s => s.CacheHits),
                CacheMisses = allSummaries.Values.Sum(s => s.CacheMisses),
                TotalPromptTokens = allSummaries.Values.Sum(s => s.TotalPromptTokens),
                TotalCompletionTokens = allSummaries.Values.Sum(s => s.TotalCompletionTokens),
                TotalTokens = allSummaries.Values.Sum(s => s.TotalTokens),
                TotalCachedTokens = allSummaries.Values.Sum(s => s.TotalCachedTokens),
                TotalTokensSaved = allSummaries.Values.Sum(s => s.TotalTokensSaved),
                TotalEstimatedCost = allSummaries.Values.Sum(s => s.TotalEstimatedCost),
                TotalCostSaved = allSummaries.Values.Sum(s => s.TotalCostSaved),
                FirstCall = allSummaries.Values.Min(s => s.FirstCall),
                LastCall = allSummaries.Values.Max(s => s.LastCall),
                AverageResponseTimeMs = (long)allSummaries.Values.Average(s => s.AverageResponseTimeMs),
                AverageConfidence = allSummaries.Values.Average(s => s.AverageConfidence)
            };
            overallSummary.HitRate = overallSummary.TotalCalls > 0 ? (double)overallSummary.CacheHits / overallSummary.TotalCalls * 100 : 0;

            var report = new AllConversationsReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalConversations = allSummaries.Count,
                ActiveConversations = activeIds.Count,
                Conversations = allSummaries,
                OverallSummary = overallSummary,
                Recommendations = new Dictionary<string, object>
                {
                    ["Total Conversations"] = allSummaries.Count,
                    ["Active Conversations (24h)"] = activeIds.Count,
                    ["Overall Hit Rate"] = $"{overallSummary.HitRate:F1}%",
                    ["Total Cost"] = $"${overallSummary.TotalEstimatedCost:F4}",
                    ["Total Cost Saved"] = $"${overallSummary.TotalCostSaved:F4}",
                    ["Most Active Conversation"] = allSummaries.Any() ? allSummaries.OrderByDescending(s => s.Value.TotalCalls).First().Key : "None"
                }
            };

            return Task.FromResult(report);
        }

        public Task<TokenUsageSummary> GetSummaryAsync(DateTime? from = null, DateTime? to = null)
        {
            var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
            var toDate = to ?? DateTime.UtcNow;

            var relevantRecords = _records.Values
                .Where(r => r.Timestamp >= fromDate && r.Timestamp <= toDate)
                .ToList();

            if (!relevantRecords.Any())
            {
                return Task.FromResult(new TokenUsageSummary
                {
                    From = fromDate,
                    To = toDate,
                    TotalCalls = 0,
                    TokensByModel = new Dictionary<string, int>(),
                    TokensByDivision = new Dictionary<string, int>(),
                    TokensByUser = new Dictionary<string, int>(),
                    TokensByConversation = new Dictionary<string, int>(),
                    CacheHitRateByDivision = new Dictionary<string, double>(),
                    RecentCalls = new List<TokenUsageRecord>()
                });
            }

            var summary = new TokenUsageSummary
            {
                From = fromDate,
                To = toDate,
                TotalCalls = relevantRecords.Count,
                SuccessfulCalls = relevantRecords.Count(r => r.IsSuccess),
                FailedCalls = relevantRecords.Count(r => !r.IsSuccess),
                CacheHits = relevantRecords.Count(r => r.FromCache),
                CacheMisses = relevantRecords.Count(r => !r.FromCache),
                TotalPromptTokens = relevantRecords.Sum(r => r.PromptTokens),
                TotalCompletionTokens = relevantRecords.Sum(r => r.CompletionTokens),
                TotalTokens = relevantRecords.Sum(r => r.TotalTokens),
                TotalCachedTokens = relevantRecords.Sum(r => r.CachedTokens),
                TotalTokensSaved = relevantRecords.Sum(r => r.TokensSaved),
                TotalEstimatedCost = relevantRecords.Sum(r => r.EstimatedCost),
                TotalCost = relevantRecords.Sum(r => r.EstimatedCost),
                TotalCostSaved = relevantRecords.Sum(r => r.EstimatedCost),
                AverageResponseTimeMs = (long)relevantRecords.Average(r => r.ResponseTimeMs),
                AverageConfidence = relevantRecords.Average(r => r.Confidence),
                TokensByModel = relevantRecords
                    .GroupBy(r => "gpt-4")
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens)),
                TokensByDivision = relevantRecords
                    .Where(r => !string.IsNullOrEmpty(r.Division))
                    .GroupBy(r => r.Division)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens)),
                TokensByUser = relevantRecords
                    .Where(r => !string.IsNullOrEmpty(r.UserId))
                    .GroupBy(r => r.UserId)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens)),
                TokensByConversation = relevantRecords
                    .Where(r => !string.IsNullOrEmpty(r.ConversationId))
                    .GroupBy(r => r.ConversationId)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens)),
                CacheHitRateByDivision = relevantRecords
                    .Where(r => !string.IsNullOrEmpty(r.Division))
                    .GroupBy(r => r.Division)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Count(r => r.FromCache) / (double)g.Count() * 100
                    ),
                RecentCalls = relevantRecords.OrderByDescending(r => r.Timestamp).Take(20).ToList()
            };

            summary.HitRate = summary.TotalCalls > 0 ? (double)summary.CacheHits / summary.TotalCalls * 100 : 0;

            return Task.FromResult(summary);
        }

        public Task ClearAsync()
        {
            _records.Clear();
            _operationOrder.Clear();
            _conversationCalls.Clear();
            _conversationLastActivity.Clear();
            _logger?.LogInformation("Token usage tracker cleared");
            return Task.CompletedTask;
        }
    }
}