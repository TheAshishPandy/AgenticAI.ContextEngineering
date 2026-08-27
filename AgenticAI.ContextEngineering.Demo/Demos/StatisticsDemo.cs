// AgenticAI.ContextEngineering.Demo/Demos/StatisticsDemo.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class StatisticsDemo : IDemo
    {
        private readonly ITokenCache _tokenCache;
        private readonly IKVCache _kvCache;

        public string Name => "Statistics Demo";
        public string Description => "Shows cache statistics for Token and KV caches";
        public bool IsConfigured => _tokenCache != null && _kvCache != null;
        public string ConfigurationStatus => _tokenCache != null && _kvCache != null ? "✅ Configured" : "❌ Not Configured";

        public StatisticsDemo(ITokenCache tokenCache, IKVCache kvCache)
        {
            _tokenCache = tokenCache;
            _kvCache = kvCache;
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

        private async Task ShowReportAsync()
        {
            Console.WriteLine("\n  📄 Token Cache Report:");
            var report = await _tokenCache.GenerateReportAsync();
            Console.WriteLine(report);
        }
    }
}