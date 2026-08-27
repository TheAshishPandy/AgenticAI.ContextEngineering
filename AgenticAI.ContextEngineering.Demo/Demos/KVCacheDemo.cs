// AgenticAI.ContextEngineering.Demo/Demos/KVCacheDemo.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class KVCacheDemo : IDemo
    {
        private readonly IKVCache _kvCache;

        public string Name => "KV Cache Demo";
        public string Description => "Tests Key-Value Cache operations (Set, Get, Exists, Remove)";
        public bool IsConfigured => _kvCache != null;
        public string ConfigurationStatus => _kvCache != null ? "✅ Configured" : "❌ Not Configured";

        public KVCacheDemo(IKVCache kvCache)
        {
            _kvCache = kvCache;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("\n📦 KV CACHE DEMO");
            Console.WriteLine(new string('─', 60));

            if (!IsConfigured)
            {
                Console.WriteLine("  ❌ IKVCache not registered");
                return;
            }

            try
            {
                Console.WriteLine($"  📋 Cache Type: {_kvCache.GetType().Name}");
                Console.WriteLine($"  📋 Cache Name: {_kvCache.Name}");
                Console.WriteLine($"  📋 Initial Count: {_kvCache.Count}");

                // Test 1: Set and Get
                await TestSetAndGetAsync();

                // Test 2: Exists
                await TestExistsAsync();

                // Test 3: Remove
                await TestRemoveAsync();

                // Test 4: GetOrSet
                await TestGetOrSetAsync();

                // Test 5: Count and Size
                await TestStatsAsync();

                Console.WriteLine("  ✅ KV Cache Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                Console.WriteLine($"     Stack: {ex.StackTrace}");
            }
        }

        private async Task TestSetAndGetAsync()
        {
            Console.WriteLine("\n  📝 Test 1: Set and Get");
            var key = $"demo:kv:test:{Guid.NewGuid():N}";
            var value = new { Id = 1, Name = "Test Item", Timestamp = DateTime.Now };

            Console.WriteLine($"    Setting: {key}");
            await _kvCache.SetAsync(key, value, TimeSpan.FromMinutes(5));
            Console.WriteLine($"    ✅ Set successful");

            Console.WriteLine($"    Getting: {key}");
            var retrieved = await _kvCache.GetAsync<dynamic>(key);
            Console.WriteLine($"    ✅ Retrieved: {(retrieved != null ? "Success" : "Failed")}");
            if (retrieved != null)
            {
                Console.WriteLine($"       └─ Value: {retrieved}");
            }
        }

        private async Task TestExistsAsync()
        {
            Console.WriteLine("\n  🔍 Test 2: Exists");
            var key = $"demo:kv:exists:{Guid.NewGuid():N}";
            var value = "Test exists value";

            await _kvCache.SetAsync(key, value);
            var exists = await _kvCache.ExistsAsync(key);
            Console.WriteLine($"    Key: {key}");
            Console.WriteLine($"    Exists: {exists} ✅");

            var nonExistentKey = "non-existent-key";
            var exists2 = await _kvCache.ExistsAsync(nonExistentKey);
            Console.WriteLine($"    Key: {nonExistentKey}");
            Console.WriteLine($"    Exists: {exists2} {(exists2 ? "⚠️ Should be false" : "✅")}");
        }

        private async Task TestRemoveAsync()
        {
            Console.WriteLine("\n  🗑️ Test 3: Remove");
            var key = $"demo:kv:remove:{Guid.NewGuid():N}";
            var value = "Test remove value";

            await _kvCache.SetAsync(key, value);
            Console.WriteLine($"    Set: {key}");

            var beforeRemove = await _kvCache.ExistsAsync(key);
            Console.WriteLine($"    Before Remove: {beforeRemove}");

            await _kvCache.RemoveAsync(key);
            Console.WriteLine($"    ✅ Removed: {key}");

            var afterRemove = await _kvCache.ExistsAsync(key);
            Console.WriteLine($"    After Remove: {afterRemove} {(afterRemove ? "⚠️ Should be false" : "✅")}");
        }

        private async Task TestGetOrSetAsync()
        {
            Console.WriteLine("\n  🔄 Test 4: GetOrSet");
            var key = $"demo:kv:getorset:{Guid.NewGuid():N}";
            var factoryCalled = false;

            Console.WriteLine($"    Key: {key}");
            var result = await _kvCache.GetOrSetAsync(key, async () =>
            {
                factoryCalled = true;
                Console.WriteLine($"       └─ Factory called (generating value)");
                await Task.Delay(50);
                return "Generated value from factory";
            });

            Console.WriteLine($"    Factory Called: {factoryCalled}");
            Console.WriteLine($"    Result: {result}");

            // Second call should use cache
            factoryCalled = false;
            var cached = await _kvCache.GetOrSetAsync(key, async () =>
            {
                factoryCalled = true;
                Console.WriteLine($"       └─ Factory called again (should NOT happen)");
                await Task.Delay(50);
                return "This should not be used";
            });

            Console.WriteLine($"    Factory Called Again: {factoryCalled} {(factoryCalled ? "⚠️" : "✅")}");
            Console.WriteLine($"    Cached Result: {cached}");
        }

        private async Task TestStatsAsync()
        {
            Console.WriteLine("\n  📊 Test 5: Stats");
            Console.WriteLine($"    Count: {_kvCache.Count}");
            Console.WriteLine($"    Size: {_kvCache.Size} bytes");
            var stats = _kvCache.GetStatistics();
            Console.WriteLine($"    Stats: {stats}");
        }
    }
}