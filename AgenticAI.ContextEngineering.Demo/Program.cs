// Program.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Extensions;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using AgenticAI.ContextEngineering.Demo.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     🧪 AgenticAI ContextEngineering - Demo               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            try
            {
                // Parse command line args
                var testToRun = args.Length > 0 ? args[0].ToLower() : "token-cache";

                // Build configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                // Setup DI
                var services = new ServiceCollection();

                // Add configuration
                services.AddSingleton<IConfiguration>(configuration);

                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });

                // ✅ Register all library services
                services.AddContextEngineering(configuration);

                // ✅ Register demo services
                services.AddScoped<DemoService>();

                var serviceProvider = services.BuildServiceProvider();

                // Print service status
                PrintServiceStatus(serviceProvider);

                // ✅ Run the selected test
                await RunSelectedTestAsync(serviceProvider, testToRun);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        static void PrintServiceStatus(IServiceProvider serviceProvider)
        {
            Console.WriteLine("📋 Service Status:");
            Console.WriteLine(new string('═', 50));

            var services = new Dictionary<Type, string>
            {
                { typeof(IAIResponseService), "AI Response Service" },
                { typeof(ITokenCache), "Token Cache" },
                { typeof(IKVCache), "KV Cache" },
                { typeof(ISearchService), "Search Service" },
                { typeof(IFaqService), "FAQ Service" },
                { typeof(ITokenOptimizedService), "Token Optimized Service" },
                { typeof(IQdrantClient), "Qdrant Client" }
            };

            foreach (var kvp in services)
            {
                try
                {
                    var service = serviceProvider.GetService(kvp.Key);
                    var isRegistered = service != null;
                    Console.WriteLine($"  {(isRegistered ? "✅" : "❌")} {kvp.Value}: {(isRegistered ? "Registered" : "NOT Available")}");
                }
                catch
                {
                    Console.WriteLine($"  ❌ {kvp.Value}: Error resolving");
                }
            }

            Console.WriteLine();
        }

        static async Task RunSelectedTestAsync(IServiceProvider serviceProvider, string testToRun)
        {
            try
            {
                var demo = serviceProvider.GetService<DemoService>();

                if (demo == null)
                {
                    Console.WriteLine("❌ DemoService not available");
                    return;
                }

                Console.WriteLine($"🚀 Running Test: {testToRun.ToUpper()}");
                Console.WriteLine(new string('═', 50));

                switch (testToRun)
                {
                    case "token-cache":
                        await demo.TestAIResponseWithCachingAsync();
                        break;

                    case "token-stats":
                        await demo.TestTokenCacheStatsAsync();
                        break;

                    case "token-clear":
                        await demo.TestClearTokenCacheAsync();
                        break;

                    case "token-compare":
                        await demo.TestCacheComparisonAsync();
                        break;

                    case "token-optimized":
                        await demo.TestTokenOptimizedServiceAsync();
                        break;

                    case "all-token":
                        await RunAllTokenTestsAsync(demo);
                        break;
                    // In Program.cs - add this to the switch
                    case "kv-test":
                        await demo.TestKVCacheDirectlyAsync();
                        break;
                    default:
                        Console.WriteLine($"❌ Unknown test: {testToRun}");
                        Console.WriteLine("   Available tests: token-cache, token-stats, token-clear, token-compare, token-optimized, all-token");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("✅ Demo completed!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task RunAllTokenTestsAsync(DemoService demo)
        {
            await demo.TestAIResponseWithCachingAsync();
            await demo.TestTokenCacheStatsAsync();
            await demo.TestCacheComparisonAsync();
            await demo.TestTokenOptimizedServiceAsync();
        }
    }
}