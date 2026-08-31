// AgenticAI.ContextEngineering.Demo/Demos/StatisticsDemo.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class StatisticsDemo : IDemo
    {
        private readonly ITokenCache _tokenCache;
        private readonly IKVCache _kvCache;
        private readonly ITokenUsageTracker _tokenUsageTracker;
        private readonly IAIResponseService _aiResponseService;

        public string Name => "Statistics Demo";
        public string Description => "Shows cache statistics for Token and KV caches with per-call token observability";
        public bool IsConfigured => _tokenCache != null && _kvCache != null;
        public string ConfigurationStatus => _tokenCache != null && _kvCache != null ? "✅ Configured" : "❌ Not Configured";

        public StatisticsDemo(
            ITokenCache tokenCache,
            IKVCache kvCache,
            ITokenUsageTracker tokenUsageTracker = null,
            IAIResponseService aiResponseService = null)
        {
            _tokenCache = tokenCache;
            _kvCache = kvCache;
            _tokenUsageTracker = tokenUsageTracker;
            _aiResponseService = aiResponseService;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("\n📊 STATISTICS DEMO");
            Console.WriteLine(new string('─', 60));

            if (!IsConfigured)
            {
                Console.WriteLine("  ❌ Caches not registered");
                return;
            }

            try
            {
                // Token Cache Statistics
                await ShowTokenCacheStatsAsync();

                // KV Cache Statistics
                await ShowKVCacheStatsAsync();

                // Multi-Tier Statistics
                await ShowMultiTierStatsAsync();

                // Per-Call Token Usage Details
                if (_tokenUsageTracker != null)
                {
                    await ShowDetailedTokenUsageAsync();
                }

                // AI Service Token Statistics (if available)
                if (_aiResponseService != null)
                {
                    await ShowAIServiceTokenStatsAsync();
                }

                // Generate Report
                await ShowReportAsync();

                Console.WriteLine("  ✅ Statistics Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                Console.WriteLine($"     Stack: {ex.StackTrace}");
            }
        }

        private async Task ShowTokenCacheStatsAsync()
        {
            Console.WriteLine("\n  📊 Token Cache Statistics:");
            var stats = _tokenCache.GetStats();

            Console.WriteLine($"     Cache Hits: {stats.CacheHits:N0}");
            Console.WriteLine($"     Cache Misses: {stats.CacheMisses:N0}");
            Console.WriteLine($"     Hit Rate: {stats.CacheHitRate:P2}");
            Console.WriteLine($"     Total Tokens Cached: {stats.TotalTokensCached:N0}");
            Console.WriteLine($"     Total Tokens Saved: {stats.TotalTokensSaved:N0}");
            Console.WriteLine($"     Cost Saved: ${stats.TotalCostSaved:F4}");
            Console.WriteLine($"     Cache Entries: {stats.CacheEntryCount:N0}");
        }

        private async Task ShowKVCacheStatsAsync()
        {
            Console.WriteLine("\n  📊 KV Cache Statistics:");
            var stats = _kvCache.GetStatistics();

            Console.WriteLine($"     Total Items: {stats.TotalItems:N0}");
            Console.WriteLine($"     Total Size: {stats.TotalSizeBytes:N0} bytes");
            Console.WriteLine($"     Cache Name: {stats.CacheName}");
            Console.WriteLine($"     Hits: {stats.Hits:N0}");
            Console.WriteLine($"     Misses: {stats.Misses:N0}");
            Console.WriteLine($"     Last Updated: {stats.LastUpdated:yyyy-MM-dd HH:mm:ss}");
        }

        private async Task ShowMultiTierStatsAsync()
        {
            Console.WriteLine("\n  📊 Multi-Tier Cache Statistics:");

            if (_kvCache is MultiTierKVCache multiTier)
            {
                var tierStats = multiTier.GetAllTierStatistics();

                foreach (var tier in tierStats)
                {
                    Console.WriteLine($"     {tier.Key}:");
                    Console.WriteLine($"        Items: {tier.Value.TotalItems:N0}");
                    Console.WriteLine($"        Size: {tier.Value.TotalSizeBytes:N0} bytes");
                    Console.WriteLine($"        Hits: {tier.Value.Hits:N0}");
                    Console.WriteLine($"        Misses: {tier.Value.Misses:N0}");
                }
            }
            else
            {
                Console.WriteLine($"     Not a MultiTier cache (Type: {_kvCache.GetType().Name})");
            }
        }

        private async Task ShowDetailedTokenUsageAsync()
        {
            Console.WriteLine("\n  📊 PER-CALL TOKEN USAGE DETAILS:");
            Console.WriteLine(new string('─', 90));

            var recentOps = await _tokenUsageTracker.GetRecentOperationsAsync(20);

            if (!recentOps.Any())
            {
                Console.WriteLine("     ℹ️ No token usage records found. Make some AI calls first.");
                return;
            }

           // Console.WriteLine($"  {'Time',-12} {'Division',-12} {'Tokens',-14} {'Cost',-14} {'Cache',-12} {'Time(ms)',-10} {'Confidence',-10}");
            Console.WriteLine(new string('─', 105));

            foreach (var op in recentOps.OrderByDescending(o => o.Timestamp).Take(15))
            {
                var timestamp = op.Timestamp.ToLocalTime().ToString("HH:mm:ss");
                var cacheStatus = op.FromCache ? $"✅ {op.CacheLevel}" : "❌ Generated";
                var division = op.Division ?? "N/A";

                Console.WriteLine(
                    $"  {timestamp,-12} " +
                    $"{division[..Math.Min(11, division.Length)],-12} " +
                    $"{op.TotalTokens,-14:N0} " +
                    $"${op.EstimatedCost,-14:F6} " +
                    $"{cacheStatus,-12} " +
                    $"{op.ResponseTimeMs,-10}ms " +
                    $"{op.Confidence,-10:F1}%"
                );

                if (!string.IsNullOrEmpty(op.Query))
                {
                    var queryPreview = op.Query.Length > 60 ?
                        op.Query[..60] + "..." :
                        op.Query;
                    Console.WriteLine($"     └─ \"{queryPreview}\"");
                }
            }

            // Get summary
            var summary = await _tokenUsageTracker.GetSummaryAsync(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow);

            if (summary.TotalCalls > 0)
            {
                Console.WriteLine($"\n  📈 Quick Summary (last 30 minutes):");
                Console.WriteLine($"     Total Calls: {summary.TotalCalls:N0}");
                Console.WriteLine($"     Cache Hit Rate: {summary.HitRate:F1}%");
                Console.WriteLine($"     Total Tokens: {summary.TotalTokens:N0}");
                Console.WriteLine($"     Total Cost: ${summary.TotalCost:F6}");
                Console.WriteLine($"     Avg Response: {summary.AverageResponseTimeMs}ms");

                if (summary.TokensByModel.Any())
                {
                    Console.WriteLine($"\n     Tokens by Model:");
                    foreach (var model in summary.TokensByModel.OrderByDescending(kv => kv.Value).Take(3))
                    {
                        Console.WriteLine($"       • {model.Key}: {model.Value:N0} tokens");
                    }
                }

                if (summary.TokensByDivision.Any())
                {
                    Console.WriteLine($"\n     Tokens by Division:");
                    foreach (var div in summary.TokensByDivision.OrderByDescending(kv => kv.Value).Take(3))
                    {
                        Console.WriteLine($"       • {div.Key}: {div.Value:N0} tokens");
                    }
                }
            }
        }

        private async Task ShowAIServiceTokenStatsAsync()
        {
            Console.WriteLine("\n  📊 AI Service Token Statistics:");

            try
            {
                var tokenStats = _aiResponseService.GetTokenStats();
                Console.WriteLine($"     Total Tokens Cached: {tokenStats.TotalTokensCached:N0}");
                Console.WriteLine($"     Total Tokens Saved: {tokenStats.TotalTokensSaved:N0}");
                Console.WriteLine($"     Cache Hit Rate: {tokenStats.CacheHitRate:F1}%");
                Console.WriteLine($"     Cache Hits: {tokenStats.CacheHits:N0}");
                Console.WriteLine($"     Cache Misses: {tokenStats.CacheMisses:N0}");
                Console.WriteLine($"     Cost Saved: ${tokenStats.TotalCostSaved:F6}");
                Console.WriteLine($"     Cache Entries: {tokenStats.CacheEntryCount:N0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"     ⚠️ Could not get AI service token stats: {ex.Message}");
            }
        }

        private async Task ShowReportAsync()
        {
            Console.WriteLine("\n  📄 Token Cache Report:");
            var report = await _tokenCache.GenerateReportAsync();
            Console.WriteLine(report);
        }
    }
}