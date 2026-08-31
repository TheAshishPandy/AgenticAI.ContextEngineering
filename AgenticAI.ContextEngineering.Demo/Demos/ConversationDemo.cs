// AgenticAI.ContextEngineering.Demo/Demos/ConversationDemo.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Services;
using System;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class ConversationDemo : IDemo
    {
        private readonly IAIResponseService _aiResponseService;
        private readonly ITokenUsageTracker _tokenUsageTracker;

        public string Name => "Conversation Demo";
        public string Description => "Makes AI calls with conversation IDs to demonstrate conversation tracking";
        public bool IsConfigured => _aiResponseService != null && _tokenUsageTracker != null;
        public string ConfigurationStatus => _aiResponseService != null && _tokenUsageTracker != null ? "✅ Configured" : "❌ Not Configured";

        public ConversationDemo(
            IAIResponseService aiResponseService,
            ITokenUsageTracker tokenUsageTracker)
        {
            _aiResponseService = aiResponseService;
            _tokenUsageTracker = tokenUsageTracker;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("\n💬 CONVERSATION DEMO");
            Console.WriteLine(new string('─', 60));

            if (!IsConfigured)
            {
                Console.WriteLine("  ❌ Services not configured");
                return;
            }

            try
            {
                // Create multiple conversations with different IDs
                await CreateConversationAsync("sales-001", "Sales Team", "What are our Q3 sales targets?");
                await CreateConversationAsync("sales-001", "Sales Team", "How are we tracking against targets?");
                await CreateConversationAsync("sales-001", "Sales Team", "What's our top performing product?");

                await CreateConversationAsync("support-001", "Support Team", "What are the most common customer issues?");
                await CreateConversationAsync("support-001", "Support Team", "How can we reduce response times?");
                await CreateConversationAsync("support-001", "Support Team", "What's our customer satisfaction score?");

                await CreateConversationAsync("engineering-001", "Engineering", "What's the status of the API project?");
                await CreateConversationAsync("engineering-001", "Engineering", "What are the main technical challenges?");
                await CreateConversationAsync("engineering-001", "Engineering", "When is the next deployment?");

                // Show conversation statistics
                await ShowConversationStatsAsync();

                Console.WriteLine("\n  ✅ Conversation Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                Console.WriteLine($"     Stack: {ex.StackTrace}");
            }
        }

        private async Task CreateConversationAsync(string conversationId, string division, string query)
        {
            var request = new AIResponseRequest
            {
                ConversationId = conversationId,
                Division = division,
                UserId = $"user-{division.ToLower().Replace(" ", "-")}",
                UserQuery = query,
                UseCache = true,
                MaxTokens = 100,
                Temperature = 0.3f
            };

            Console.WriteLine($"\n  📝 [{conversationId}] {query}");
            var response = await _aiResponseService.GenerateResponseAsync(request);

            if (response.IsSuccess)
            {
                var preview = response.Response?.Length > 60 ? response.Response[..60] + "..." : response.Response;
                Console.WriteLine($"     ✅ {preview}");
                Console.WriteLine($"     📊 Tokens: {response.TokenCount}, From Cache: {(response.FromCache ? "Yes" : "No")}");
            }
            else
            {
                Console.WriteLine($"     ❌ Error: {response.Error}");
            }

            // Small delay to avoid rate limiting
            await Task.Delay(500);
        }

        private async Task ShowConversationStatsAsync()
        {
            Console.WriteLine("\n\n📊 CONVERSATION STATISTICS");
            Console.WriteLine(new string('─', 60));

            var allConversations = await _tokenUsageTracker.GetAllConversationSummariesAsync();

            if (!allConversations.Any())
            {
                Console.WriteLine("  ℹ️ No conversations found.");
                return;
            }

            Console.WriteLine($"  Total Conversations: {allConversations.Count}\n");

            foreach (var conv in allConversations)
            {
                var summary = conv.Value;
                Console.WriteLine($"  📋 Conversation: {conv.Key}");
                Console.WriteLine($"     Division: {summary.Division ?? "Unknown"}");
                Console.WriteLine($"     Calls: {summary.TotalCalls}");
                Console.WriteLine($"     Tokens: {summary.TotalTokens:N0}");
                Console.WriteLine($"     Cost: ${summary.TotalEstimatedCost:F4}");
                Console.WriteLine($"     Cache Hit Rate: {summary.HitRate:F1}%");
                Console.WriteLine($"     Avg Response: {summary.AverageResponseTimeMs}ms");
                Console.WriteLine($"     First Call: {summary.FirstCall.ToLocalTime():HH:mm:ss}");
                Console.WriteLine($"     Last Call: {summary.LastCall.ToLocalTime():HH:mm:ss}");

                if (summary.TokensByUser.Any())
                {
                    Console.WriteLine($"     Users: {string.Join(", ", summary.TokensByUser.Keys)}");
                }
                Console.WriteLine();
            }

            // Show active conversations
            var activeIds = await _tokenUsageTracker.GetActiveConversationIdsAsync(TimeSpan.FromMinutes(30));
            if (activeIds.Any())
            {
                Console.WriteLine($"  🔄 Active Conversations (last 30 min): {string.Join(", ", activeIds)}");
            }

            // Generate and show report for the first conversation
            if (allConversations.Any())
            {
                var firstConvId = allConversations.First().Key;
                var report = await _tokenUsageTracker.GenerateConversationReportAsync(firstConvId);

                Console.WriteLine($"\n  📄 Report for: {firstConvId}");
                Console.WriteLine("  " + new string('─', 50));
                foreach (var rec in report.Recommendations)
                {
                    Console.WriteLine($"     • {rec.Key}: {rec.Value}");
                }
            }
        }
    }
}