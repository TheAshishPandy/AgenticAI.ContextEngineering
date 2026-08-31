// AgenticAI.ContextEngineering.Demo/Demos/ClearCacheDemo.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class ClearCacheDemo : IDemo
    {
        private readonly ITokenCache _tokenCache;
        private readonly IKVCache _kvCache;

        public string Name => "Clear Cache Demo";
        public string Description => "Clears all caches and shows before/after statistics";
        public bool IsConfigured => _tokenCache != null && _kvCache != null;
        public string ConfigurationStatus => _tokenCache != null && _kvCache != null ? "✅ Configured" : "❌ Not Configured";

        public ClearCacheDemo(ITokenCache tokenCache, IKVCache kvCache)
        {
            _tokenCache = tokenCache;
            _kvCache = kvCache;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("\n🗑️ CLEAR CACHE DEMO");
            Console.WriteLine(new string('─', 60));

            if (!IsConfigured)  
            {
                Console.WriteLine("  ❌ Caches not registered");
                return;
            }

            try
            {
                // Show before stats
                await ShowBeforeStatsAsync();

                // Clear token cache
                Console.WriteLine("\n  🗑️ Clearing Token Cache...");
                _tokenCache.Clear();
                Console.WriteLine("  ✅ Token Cache Cleared!");

                // Clear KV cache
                Console.WriteLine("  🗑️ Clearing KV Cache...");
                await _kvCache.ClearAsync();
                Console.WriteLine("  ✅ KV Cache Cleared!");

                // Show after stats
                await ShowAfterStatsAsync();

                Console.WriteLine("  ✅ Clear Cache Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                Console.WriteLine($"     Stack: {ex.StackTrace}");
            }
        }

        private async Task ShowBeforeStatsAsync()
        {
            Console.WriteLine("\n  📊 Before Clear:");

            // Token cache stats
            var tokenStats = _tokenCache.GetStats();
            Console.WriteLine($"     Token Cache:");
            Console.WriteLine($"        Tokens Cached: {tokenStats.TotalTokensCached:N0}");
            Console.WriteLine($"        Tokens Saved: {tokenStats.TotalTokensSaved:N0}");
            Console.WriteLine($"        Cache Entries: {tokenStats.CacheEntryCount:N0}");

            // KV cache stats
            var kvStats = _kvCache.GetStatistics();
            Console.WriteLine($"     KV Cache:");
            Console.WriteLine($"        Total Items: {kvStats.TotalItems:N0}");
            Console.WriteLine($"        Total Size: {kvStats.TotalSizeBytes:N0} bytes");
        }

        private async Task ShowAfterStatsAsync()
        {
            Console.WriteLine("\n  📊 After Clear:");

            // Token cache stats
            var tokenStats = _tokenCache.GetStats();
            Console.WriteLine($"     Token Cache:");
            Console.WriteLine($"        Tokens Cached: {tokenStats.TotalTokensCached:N0}");
            Console.WriteLine($"        Tokens Saved: {tokenStats.TotalTokensSaved:N0}");
            Console.WriteLine($"        Cache Entries: {tokenStats.CacheEntryCount:N0}");

            // KV cache stats
            var kvStats = _kvCache.GetStatistics();
            Console.WriteLine($"     KV Cache:");
            Console.WriteLine($"        Total Items: {kvStats.TotalItems:N0}");
            Console.WriteLine($"        Total Size: {kvStats.TotalSizeBytes:N0} bytes");

            // Verify cleared
            if (tokenStats.TotalTokensCached == 0 && tokenStats.CacheEntryCount == 0 && kvStats.TotalItems == 0)
            {
                Console.WriteLine($"     ✅ All caches cleared successfully!");
            }
            else
            {
                Console.WriteLine($"     ⚠️ Some caches may not have been fully cleared");
            }
        }
    }
}