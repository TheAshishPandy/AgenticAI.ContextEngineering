// AgenticAI.ContextEngineering.Demo/Demos/ConversationStatsDemo.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Services;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class ConversationStatsDemo : IDemo
    {
        private readonly ITokenUsageTracker _tokenUsageTracker;
        private readonly IAIResponseService _aiResponseService;

        public string Name => "Conversation Stats Demo";
        public string Description => "Shows detailed token usage statistics per conversation with rich visualization";
        public bool IsConfigured => _tokenUsageTracker != null;
        public string ConfigurationStatus => _tokenUsageTracker != null ? "✅ Configured" : "❌ Not Configured";

        public ConversationStatsDemo(
            ITokenUsageTracker tokenUsageTracker,
            IAIResponseService aiResponseService = null)
        {
            _tokenUsageTracker = tokenUsageTracker;
            _aiResponseService = aiResponseService;
        }

        public async Task RunAsync()
        {
            Console.Clear();
            Console.WriteLine("\n💬 CONVERSATION TOKEN USAGE DASHBOARD");
            Console.WriteLine(new string('═', 80));

            if (!IsConfigured)
            {
                Console.WriteLine("  ❌ Token Usage Tracker not registered");
                return;
            }

            try
            {
                await ShowQuickOverviewAsync();
                await ShowAllConversationsSummaryAsync();
                await ShowTopConversationDetailsAsync();
                await ShowTokenDistributionAsync();
                await ShowRecommendationsAsync();

                Console.WriteLine("\n  ✅ Conversation Statistics Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                Console.WriteLine($"     Stack: {ex.StackTrace}");
            }
        }

        private async Task ShowQuickOverviewAsync()
        {
            Console.WriteLine("\n📊 QUICK OVERVIEW");
            Console.WriteLine(new string('─', 60));

            var allConversations = await _tokenUsageTracker.GetAllConversationSummariesAsync();
            var activeIds = await _tokenUsageTracker.GetActiveConversationIdsAsync();

            if (!allConversations.Any())
            {
                Console.WriteLine("  ℹ️ No conversations found. Make some AI calls first.");
                return;
            }

            var totalCalls = allConversations.Values.Sum(s => s.TotalCalls);
            var totalTokens = allConversations.Values.Sum(s => s.TotalTokens);
            var totalCost = allConversations.Values.Sum(s => s.TotalEstimatedCost);
            var totalCacheHits = allConversations.Values.Sum(s => s.CacheHits);
            var hitRate = totalCalls > 0 ? (double)totalCacheHits / totalCalls * 100 : 0;

            var topConversation = allConversations
                .OrderByDescending(kvp => kvp.Value.TotalTokens)
                .FirstOrDefault();

            Console.WriteLine($@"
  📈 Global Statistics:
     Active Conversations:    {activeIds.Count(),3}
     Total Conversations:     {allConversations.Count,3}
     Total Calls:            {totalCalls,6:N0}
     Total Tokens:           {totalTokens,8:N0}
     Total Cost:            ${totalCost,10:F4}
     Cache Hit Rate:        {hitRate,7:F1}%
     
  🏆 Top Conversation:
     ID: {topConversation.Key?[..Math.Min(20, topConversation.Key?.Length ?? 0)]}...
     Calls: {topConversation.Value?.TotalCalls ?? 0,4}
     Tokens: {topConversation.Value?.TotalTokens ?? 0,7:N0}
     Cost: ${topConversation.Value?.TotalEstimatedCost ?? 0:F4}");
        }

        private async Task ShowAllConversationsSummaryAsync()
        {
            Console.WriteLine("\n📋 ALL CONVERSATIONS SUMMARY");
            Console.WriteLine(new string('─', 80));

            var allConversations = await _tokenUsageTracker.GetAllConversationSummariesAsync();

            if (!allConversations.Any())
            {
                Console.WriteLine("  ℹ️ No conversations found.");
                return;
            }

            var table = new StringBuilder();
            table.AppendLine($"  {"ID",-8} {"Calls",-8} {"Tokens",-12} {"Cost",-12} {"Cache Hit",-12} {"Last Active",-20}");
            table.AppendLine(new string('─', 80));

            foreach (var conv in allConversations
                .OrderByDescending(kvp => kvp.Value.LastCall)
                .Take(15))
            {
                var summary = conv.Value;
                var id = conv.Key.Length > 20 ? conv.Key[..20] + "..." : conv.Key;
                var lastActive = summary.LastCall.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                table.AppendLine(
                    $"  {id,-8} " +
                    $"{summary.TotalCalls,-8} " +
                    $"{summary.TotalTokens,-12:N0} " +
                    $"${summary.TotalEstimatedCost,-12:F4} " +
                    $"{summary.HitRate,-12:F1}% " +
                    $"{lastActive,-20}"
                );
            }

            Console.WriteLine(table.ToString());
        }

        private async Task ShowTopConversationDetailsAsync()
        {
            Console.WriteLine("\n🔍 TOP CONVERSATION DETAILS");
            Console.WriteLine(new string('─', 80));

            var allConversations = await _tokenUsageTracker.GetAllConversationSummariesAsync();

            if (!allConversations.Any())
            {
                Console.WriteLine("  ℹ️ No conversations found.");
                return;
            }

            var topThree = allConversations
                .OrderByDescending(kvp => kvp.Value.TotalTokens)
                .Take(3)
                .ToList();

            for (int i = 0; i < topThree.Count; i++)
            {
                var conv = topThree[i];
                var summary = conv.Value;

                Console.WriteLine($"\n  {i + 1}. Conversation: {conv.Key}");
                Console.WriteLine($"     Division: {summary.Division ?? "Unknown"}");
                Console.WriteLine(new string(' ', 4) + new string('─', 70));

                var maxTokens = topThree.Max(t => t.Value.TotalTokens);
                var barLength = maxTokens > 0 ? (int)((double)summary.TotalTokens / maxTokens * 40) : 0;
                var tokenBar = new string('█', barLength);

                Console.WriteLine($@"
     📊 Token Usage:
        Total Tokens:     {summary.TotalTokens,8:N0} [{tokenBar}] {(summary.TotalTokens == maxTokens ? "🏆" : "")}
        Prompt Tokens:    {summary.TotalPromptTokens,8:N0}
        Completion Tokens:{summary.TotalCompletionTokens,8:N0}
        Cached Tokens:    {summary.TotalCachedTokens,8:N0}
        Tokens Saved:     {summary.TotalTokensSaved,8:N0}

     💰 Cost Analysis:
        Total Cost:       ${summary.TotalEstimatedCost,10:F4}
        Cost Saved:       ${summary.TotalCostSaved,10:F4}
        Avg Cost/Call:    ${(summary.TotalCalls > 0 ? summary.TotalEstimatedCost / summary.TotalCalls : 0),10:F4}

     ⚡ Performance:
        Calls:            {summary.TotalCalls,8}
        Cache Hit Rate:   {summary.HitRate,8:F1}%
        Avg Response:     {summary.AverageResponseTimeMs,8}ms
        Avg Confidence:   {summary.AverageConfidence,8:F1}%

     ⏰ Activity:
        First Call:       {summary.FirstCall.ToLocalTime():yyyy-MM-dd HH:mm}
        Last Call:        {summary.LastCall.ToLocalTime():yyyy-MM-dd HH:mm}
        Duration:         {(summary.LastCall - summary.FirstCall).TotalHours:F1} hours");

                if (summary.RecentCalls.Any())
                {
                    Console.WriteLine($"\n     🔄 Recent Queries (Last {Math.Min(3, summary.RecentCalls.Count)}):");
                    foreach (var call in summary.RecentCalls.Take(3))
                    {
                        var queryPreview = call.Query?.Length > 50 ? call.Query[..50] + "..." : call.Query;
                        var status = call.FromCache ? "✅" : "❌";
                        Console.WriteLine($"        {status} {call.Timestamp.ToLocalTime():HH:mm:ss} | " +
                                         $"{call.TotalTokens,4} tokens | " +
                                         $"\"{queryPreview}\"");
                    }
                }
            }
        }

        private async Task ShowTokenDistributionAsync()
        {
            Console.WriteLine("\n📊 TOKEN DISTRIBUTION ANALYSIS");
            Console.WriteLine(new string('─', 80));

            var allConversations = await _tokenUsageTracker.GetAllConversationSummariesAsync();

            if (!allConversations.Any())
            {
                Console.WriteLine("  ℹ️ No conversations found.");
                return;
            }

            var totalTokens = allConversations.Values.Sum(s => s.TotalTokens);
            var totalCalls = allConversations.Values.Sum(s => s.TotalCalls);

            Console.WriteLine($@"
  📈 Distribution by Conversation (Top 5):");

            var topByTokens = allConversations
                .OrderByDescending(kvp => kvp.Value.TotalTokens)
                .Take(5)
                .ToList();

            foreach (var conv in topByTokens)
            {
                var percentage = totalTokens > 0 ? (double)conv.Value.TotalTokens / totalTokens * 100 : 0;
                var barLength = (int)(percentage / 2);
                var bar = new string('▓', barLength);

                Console.WriteLine($"     {conv.Key[..Math.Min(15, conv.Key.Length)],-15} " +
                                 $"{conv.Value.TotalTokens,8:N0} tokens " +
                                 $"({percentage,5:F1}%) {bar}");
            }

            var cacheHits = allConversations.Values.Sum(s => s.CacheHits);
            var cacheMisses = allConversations.Values.Sum(s => s.CacheMisses);
            var totalRequests = cacheHits + cacheMisses;

            Console.WriteLine($@"
  🎯 Cache Efficiency:
     Cache Hits:    {cacheHits,8:N0} ({(totalRequests > 0 ? (double)cacheHits / totalRequests * 100 : 0):F1}%)
     Cache Misses:  {cacheMisses,8:N0} ({(totalRequests > 0 ? (double)cacheMisses / totalRequests * 100 : 0):F1}%)");

            if (totalRequests > 0)
            {
                var hitRatio = (double)cacheHits / totalRequests;
                var hitBarLength = (int)(hitRatio * 40);
                var missBarLength = 40 - hitBarLength;
                var hitBar = new string('█', hitBarLength);
                var missBar = new string('░', missBarLength);
                Console.WriteLine($"     [{hitBar}{missBar}]");
            }

            var totalCost = allConversations.Values.Sum(s => s.TotalEstimatedCost);
            Console.WriteLine($@"
  💰 Cost Distribution:
     Total Cost: ${totalCost,10:F4}
     Avg Cost per Call: ${(totalCalls > 0 ? totalCost / totalCalls : 0),10:F4}");
        }

        private async Task ShowRecommendationsAsync()
        {
            Console.WriteLine("\n💡 RECOMMENDATIONS");
            Console.WriteLine(new string('─', 80));

            var allConversations = await _tokenUsageTracker.GetAllConversationSummariesAsync();

            if (!allConversations.Any())
            {
                Console.WriteLine("  ℹ️ No conversations found to analyze.");
                return;
            }

            var totalCalls = allConversations.Values.Sum(s => s.TotalCalls);
            var totalCacheHits = allConversations.Values.Sum(s => s.CacheHits);
            var hitRate = totalCalls > 0 ? (double)totalCacheHits / totalCalls * 100 : 0;

            Console.WriteLine("\n  Based on the analysis of your conversations:");

            if (hitRate < 60)
            {
                Console.WriteLine("  🔴 Cache hit rate is low (< 60%). Consider:");
                Console.WriteLine("     • Increase cache expiration time");
                Console.WriteLine("     • Enable caching for more query types");
                Console.WriteLine("     • Use consistent query patterns");
            }
            else if (hitRate < 80)
            {
                Console.WriteLine("  🟡 Cache hit rate is moderate (60-80%). Consider:");
                Console.WriteLine("     • Fine-tune cache key generation");
                Console.WriteLine("     • Add more frequently asked questions to FAQ");
            }
            else
            {
                Console.WriteLine("  🟢 Excellent cache hit rate! (> 80%). Keep it up!");
            }

            var highCostConversations = allConversations
                .Where(kvp => kvp.Value.TotalEstimatedCost > 0.01m)
                .OrderByDescending(kvp => kvp.Value.TotalEstimatedCost)
                .Take(3)
                .ToList();

            if (highCostConversations.Any())
            {
                Console.WriteLine("\n  💰 High-cost conversations to optimize:");
                foreach (var conv in highCostConversations)
                {
                    Console.WriteLine($"     • {conv.Key[..Math.Min(20, conv.Key.Length)]}: ${conv.Value.TotalEstimatedCost:F4}");
                }
            }

            var longConversations = allConversations
                .Where(kvp => kvp.Value.TotalCalls > 10)
                .OrderByDescending(kvp => kvp.Value.TotalCalls)
                .Take(3)
                .ToList();

            if (longConversations.Any())
            {
                Console.WriteLine("\n  📝 Long-running conversations to review:");
                foreach (var conv in longConversations)
                {
                    Console.WriteLine($"     • {conv.Key[..Math.Min(20, conv.Key.Length)]}: {conv.Value.TotalCalls} calls, {conv.Value.TotalTokens:N0} tokens");
                }
            }

            Console.WriteLine("\n  📊 Summary Statistics:");
            Console.WriteLine($"     Total Conversations: {allConversations.Count}");
            Console.WriteLine($"     Average Tokens per Conversation: {(allConversations.Any() ? allConversations.Values.Average(s => s.TotalTokens) : 0):N0}");
            Console.WriteLine($"     Average Cost per Conversation: ${(allConversations.Any() ? allConversations.Values.Average(s => (double)s.TotalEstimatedCost) : 0):F4}");
            Console.WriteLine($"     Total Cost Saved: ${allConversations.Values.Sum(s => s.TotalCostSaved):F4}");
        }
    }
}