// AgenticAI.ContextEngineering.Demo/Services/DemoRunner.cs
using AgenticAI.ContextEngineering.Demo.Demos;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Services
{
    public class DemoRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _demoRegistry;

        public DemoRunner(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _demoRegistry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                // Main demos
                { "kv", typeof(KVCacheDemo) },
                { "token", typeof(TokenCacheDemo) },
                { "ai", typeof(AIResponseDemo) },
                { "search", typeof(SearchDemo) },
                { "faq", typeof(FAQDemo) },
                { "optimized", typeof(TokenOptimizedDemo) },
                { "stats", typeof(StatisticsDemo) },
                { "clear", typeof(ClearCacheDemo) },
                { "conv", typeof(ConversationStatsDemo) },  // NEW
                { "conversation", typeof(ConversationStatsDemo) },  // NEW
                { "conv-stats", typeof(ConversationStatsDemo) },  // NEW
                { "conversations", typeof(ConversationStatsDemo) },  // NEW
                
                // Legacy/backward compatibility
                { "kv-test", typeof(KVCacheDemo) },
                { "token-cache", typeof(AIResponseDemo) },
                { "token-stats", typeof(StatisticsDemo) },
                { "token-clear", typeof(ClearCacheDemo) },
                { "token-compare", typeof(AIResponseDemo) },
                { "token-optimized", typeof(TokenOptimizedDemo) }
            };
        }

        public async Task RunDemoAsync<T>() where T : IDemo
        {
            try
            {
                var demo = _serviceProvider.GetService<T>();
                if (demo == null)
                {
                    Console.WriteLine($"❌ Demo {typeof(T).Name} not found");
                    return;
                }

                if (!demo.IsConfigured)
                {
                    Console.WriteLine($"❌ Demo {demo.Name} is not configured. Status: {demo.ConfigurationStatus}");
                    return;
                }

                Console.WriteLine($"\n📌 {demo.Name}");
                Console.WriteLine($"   {demo.Description}");
                Console.WriteLine($"   Status: {demo.ConfigurationStatus}");

                await demo.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error running demo {typeof(T).Name}: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
            }
        }

        public async Task RunDemoByKeyAsync(string key)
        {
            if (!_demoRegistry.TryGetValue(key, out var demoType))
            {
                Console.WriteLine($"❌ Demo '{key}' not found. Use 'list' to see available demos.");
                return;
            }

            var demo = _serviceProvider.GetService(demoType) as IDemo;
            if (demo == null)
            {
                Console.WriteLine($"❌ Could not instantiate demo '{key}'");
                return;
            }

            await RunDemoAsync(demo);
        }

        private async Task RunDemoAsync(IDemo demo)
        {
            try
            {
                if (!demo.IsConfigured)
                {
                    Console.WriteLine($"❌ Demo {demo.Name} is not configured. Status: {demo.ConfigurationStatus}");
                    return;
                }

                Console.WriteLine($"\n📌 {demo.Name}");
                Console.WriteLine($"   {demo.Description}");
                Console.WriteLine($"   Status: {demo.ConfigurationStatus}");

                await demo.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error running demo {demo.Name}: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
            }
        }

        public async Task RunAllDemosAsync()
        {
            Console.WriteLine("\n══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  🚀 AGENTICAI CONTEXT ENGINEERING - COMPLETE DEMO");
            Console.WriteLine("══════════════════════════════════════════════════════════════════════\n");

            var demos = new List<IDemo>();
            var allTypes = new[]
            {
                typeof(KVCacheDemo),
                typeof(TokenCacheDemo),
                typeof(AIResponseDemo),
                typeof(SearchDemo),
                typeof(FAQDemo),
                typeof(TokenOptimizedDemo),
                typeof(StatisticsDemo),
                typeof(ConversationStatsDemo),  // NEW
                typeof(ClearCacheDemo)
            };

            int passed = 0;
            int failed = 0;
            int skipped = 0;

            foreach (var type in allTypes)
            {
                try
                {
                    var demo = _serviceProvider.GetService(type) as IDemo;
                    if (demo == null)
                    {
                        Console.WriteLine($"❌ Could not resolve {type.Name}");
                        failed++;
                        continue;
                    }

                    if (!demo.IsConfigured)
                    {
                        Console.WriteLine($"\n⚠️ Skipping {demo.Name} - {demo.ConfigurationStatus}");
                        skipped++;
                        continue;
                    }

                    Console.WriteLine($"\n📌 {demo.Name}");
                    Console.WriteLine($"   {demo.Description}");
                    Console.WriteLine($"   Status: {demo.ConfigurationStatus}");

                    await demo.RunAsync();
                    passed++;
                    Console.WriteLine($"   ✅ {demo.Name} completed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error running {type.Name}: {ex.Message}");
                    failed++;
                }
            }

            Console.WriteLine("\n══════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"  📊 Summary: {passed} passed, {failed} failed, {skipped} skipped");
            Console.WriteLine("══════════════════════════════════════════════════════════════════════");
        }

        public void ListDemos()
        {
            Console.WriteLine("\n📋 Available Demos:");
            Console.WriteLine(new string('═', 60));

            var maxKeyLength = _demoRegistry.Keys.Max(k => k.Length);
            foreach (var kvp in _demoRegistry.OrderBy(k => k.Key))
            {
                try
                {
                    var demo = _serviceProvider.GetService(kvp.Value) as IDemo;
                    var status = demo?.IsConfigured == true ? "✅" : "❌";
                    var name = demo?.Name ?? kvp.Value.Name;
                    Console.WriteLine($"  {kvp.Key.PadRight(maxKeyLength + 2)} {status} {name}");
                }
                catch
                {
                    Console.WriteLine($"  {kvp.Key.PadRight(maxKeyLength + 2)} ⚠️ {kvp.Value.Name}");
                }
            }

            Console.WriteLine("\n💡 Usage: dotnet run -- <command>");
            Console.WriteLine("   Example: dotnet run -- conv");
            Console.WriteLine("   Example: dotnet run -- all");
        }
    }
}