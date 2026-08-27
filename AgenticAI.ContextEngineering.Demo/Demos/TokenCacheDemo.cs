// AgenticAI.ContextEngineering.Demo/Demos/TokenCacheDemo.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class TokenCacheDemo : IDemo
    {
        private readonly ITokenCache _tokenCache;

        public string Name => "Token Cache Demo";
        public string Description => "Tests Token Cache operations (GetOrAdd, TryGet, Stats)";
        public bool IsConfigured => _tokenCache != null;
        public string ConfigurationStatus => _tokenCache != null ? "✅ Configured" : "❌ Not Configured";

        public TokenCacheDemo(ITokenCache tokenCache)
        {
            _tokenCache = tokenCache;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("\n🔐 TOKEN CACHE DEMO");
            Console.WriteLine(new string('─', 60));

            if (!IsConfigured)
            {
                Console.WriteLine("  ❌ ITokenCache not registered");
                return;
            }

            try
            {
                Console.WriteLine($"  📋 Cache Type: {_tokenCache.GetType().Name}");

                // Test 1: Set and TryGet
                await TestSetAndTryGetAsync();

                // Test 2: GetOrAdd
                await TestGetOrAddAsync();

                // Test 3: GetOrAdd with Token Tracking
                await TestGetOrAddWithTokensAsync();

                // Test 4: Remove
                await TestRemoveAsync();

                // Test 5: Stats
                await TestStatsAsync();

                Console.WriteLine("  ✅ Token Cache Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                Console.WriteLine($"     Stack: {ex.StackTrace}");
            }
        }

        private async Task TestSetAndTryGetAsync()
        {
            Console.WriteLine("\n  📝 Test 1: Set and TryGet");
            var key = $"demo:token:set:{Guid.NewGuid():N}";
            var value = "This is a test token cached value";

            Console.WriteLine($"    Setting: {key}");
            _tokenCache.Set(key, value, tokenCount: 25);
            Console.WriteLine($"    ✅ Set successful");

            Console.WriteLine($"    Getting: {key}");
            var retrieved = _tokenCache.TryGet<string>(key, out var result);
            Console.WriteLine($"    ✅ Retrieved: {retrieved}");
            if (retrieved)
            {
                Console.WriteLine($"       └─ Value: {result}");
            }
        }

        private async Task TestGetOrAddAsync()
        {
            Console.WriteLine("\n  🔄 Test 2: GetOrAdd");
            var key = $"demo:token:getoradd:{Guid.NewGuid():N}";
            var factoryCalled = false;

            Console.WriteLine($"    Key: {key}");
            var result = await _tokenCache.GetOrAddAsync<string>(
                key,
                async () =>
                {
                    factoryCalled = true;
                    Console.WriteLine($"       └─ Factory called (generating value)");
                    await Task.Delay(50);
                    return "Generated value from factory";
                }
            );

            Console.WriteLine($"    Factory Called: {factoryCalled}");
            Console.WriteLine($"    Result: {result}");

            // Second call should use cache
            factoryCalled = false;
            var cached = await _tokenCache.GetOrAddAsync<string>(
                key,
                async () =>
                {
                    factoryCalled = true;
                    Console.WriteLine($"       └─ Factory called again (should NOT happen)");
                    await Task.Delay(50);
                    return "This should not be used";
                }
            );

            Console.WriteLine($"    Factory Called Again: {factoryCalled} {(factoryCalled ? "⚠️" : "✅")}");
            Console.WriteLine($"    Cached Result: {cached}");
        }

        private async Task TestGetOrAddWithTokensAsync()
        {
            Console.WriteLine("\n  💰 Test 3: GetOrAdd with Token Tracking");
            var key = $"demo:token:tokens:{Guid.NewGuid():N}";
            var tokenCount = 42;
            var costPerToken = 0.000001m;

            Console.WriteLine($"    Key: {key}");
            Console.WriteLine($"    Token Count: {tokenCount}");
            Console.WriteLine($"    Cost Per Token: ${costPerToken:F6}");

            var result = await _tokenCache.GetOrAddAsync<string>(
                key,
                async () =>
                {
                    Console.WriteLine($"       └─ Factory called");
                    await Task.Delay(50);
                    return "Token tracked value";
                },
                tokenCount: tokenCount,
                costPerToken: costPerToken
            );

            Console.WriteLine($"    Result: {result}");
        }

        private async Task TestRemoveAsync()
        {
            Console.WriteLine("\n  🗑️ Test 4: Remove");
            var key = $"demo:token:remove:{Guid.NewGuid():N}";
            var value = "Test remove value";

            _tokenCache.Set(key, value);
            Console.WriteLine($"    Set: {key}");

            var beforeRemove = _tokenCache.TryGet<string>(key, out _);
            Console.WriteLine($"    Before Remove: {beforeRemove}");

            _tokenCache.Remove(key);
            Console.WriteLine($"    ✅ Removed: {key}");

            var afterRemove = _tokenCache.TryGet<string>(key, out _);
            Console.WriteLine($"    After Remove: {afterRemove} {(afterRemove ? "⚠️ Should be false" : "✅")}");
        }

        private async Task TestStatsAsync()
        {
            Console.WriteLine("\n  📊 Test 5: Stats");
            var stats = _tokenCache.GetStats();
            Console.WriteLine($"    Cache Hits: {stats.CacheHits}");
            Console.WriteLine($"    Cache Misses: {stats.CacheMisses}");
            Console.WriteLine($"    Hit Rate: {stats.CacheHitRate:P2}");
            Console.WriteLine($"    Tokens Cached: {stats.TotalTokensCached}");
            Console.WriteLine($"    Tokens Saved: {stats.TotalTokensSaved}");
            Console.WriteLine($"    Cost Saved: ${stats.TotalCostSaved:F4}");
            Console.WriteLine($"    Cache Entries: {stats.CacheEntryCount}");
        }
    }
}