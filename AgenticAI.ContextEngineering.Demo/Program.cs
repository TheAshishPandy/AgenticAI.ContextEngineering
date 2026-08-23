// Program.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Extensions;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using AgenticAI.ContextEngineering.Core.Services;
using AgenticAI.ContextEngineering.Demo.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
                var testToRun = args.Length > 0 ? args[0].ToLower() : "search";

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
                services.AddScoped<SearchDemo>();
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
                { typeof(IQdrantClient), "Qdrant Client" },
                { typeof(SearchIndex), "Search Index" },
                { typeof(HybridSearchEngine), "Hybrid Search Engine" },
                { typeof(LexicalSearch), "Lexical Search" },
                { typeof(SemanticSearch), "Semantic Search" },
                { typeof(IEmbeddingGenerator), "Embedding Generator" },
                { typeof(ISearchService), "Search Service" },
                { typeof(CachedSearchService), "Cached Search Service" },
                { typeof(IAIResponseService), "AI Response Service" },
                { typeof(IFaqService), "FAQ Service" },
                { typeof(ITokenOptimizedService), "Token Optimized Service" },
                { typeof(IKVCache), "KV Cache" },
                { typeof(ITokenCache), "Token Cache" }
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

            // Check Qdrant collection
            try
            {
                var qdrant = serviceProvider.GetService<IQdrantClient>();
                if (qdrant != null)
                {
                    var size = qdrant.GetCollectionSizeAsync().Result;
                    Console.WriteLine($"  📊 Qdrant documents: {size}");
                }
            }
            catch { /* Ignore */ }

            Console.WriteLine();
        }

        static async Task RunSelectedTestAsync(IServiceProvider serviceProvider, string testToRun)
        {
            try
            {
                var demo = serviceProvider.GetService<DemoService>();
                var searchDemo = serviceProvider.GetService<SearchDemo>();

                if (demo == null)
                {
                    Console.WriteLine("❌ DemoService not available");
                    return;
                }

                Console.WriteLine($"🚀 Running Test: {testToRun.ToUpper()}");
                Console.WriteLine(new string('═', 50));

                switch (testToRun)
                {
                    case "search":
                        if (searchDemo != null)
                        {
                            await searchDemo.RunAsync();
                        }
                        else
                        {
                            Console.WriteLine("❌ SearchDemo not available");
                        }
                        break;

                    case "ai":
                        await demo.TestAIResponseAsync();
                        break;

                    case "faq":
                        await demo.TestFaqServiceAsync();
                        break;

                    case "token":
                        await demo.TestTokenOptimizedServiceAsync();
                        break;

                    case "rag":
                        await demo.TestRAGPipelineAsync();
                        break;

                    case "batch":
                        await demo.TestBatchProcessingAsync();
                        break;

                    case "stream":
                        await demo.TestStreamingAsync();
                        break;

                    case "caching":
                        await demo.TestCachingAsync();
                        break;

                    case "services":
                        await demo.TestServiceRegistrationAsync();
                        break;

                    case "all":
                        await RunAllDemosAsync(demo, searchDemo);
                        break;

                    default:
                        Console.WriteLine($"❌ Unknown test: {testToRun}");
                        Console.WriteLine("   Available tests: search, ai, faq, token, rag, batch, stream, caching, services, all");
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

        static async Task RunAllDemosAsync(DemoService demo, SearchDemo searchDemo)
        {
            var demos = new List<(string Name, Func<Task> Action, bool IsAvailable)>
            {
                ("Search Demo", async () => { if (searchDemo != null) await searchDemo.RunAsync(); }, searchDemo != null),
                ("Service Registration", async () => await demo.TestServiceRegistrationAsync(), true),
                ("AI Response", async () => await demo.TestAIResponseAsync(), true),
                ("FAQ Service", async () => await demo.TestFaqServiceAsync(), true),
                ("Token Optimized", async () => await demo.TestTokenOptimizedServiceAsync(), true),
                ("RAG Pipeline", async () => await demo.TestRAGPipelineAsync(), true),
                ("Batch Processing", async () => await demo.TestBatchProcessingAsync(), true),
                ("Streaming", async () => await demo.TestStreamingAsync(), true),
                ("Caching", async () => await demo.TestCachingAsync(), true)
            };

            var total = demos.Count;
            var completed = 0;

            foreach (var (name, action, isAvailable) in demos)
            {
                if (!isAvailable)
                {
                    Console.WriteLine($"\n⏭️ Skipping {name} (not available)");
                    continue;
                }

                Console.WriteLine($"\n🔄 Running {name}...");
                Console.WriteLine(new string('─', 40));

                try
                {
                    await action();
                    completed++;
                    Console.WriteLine($"✅ {name} completed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ {name} failed: {ex.Message}");
                }
            }

            Console.WriteLine("\n╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║  ✅ Demos Completed: {completed}/{total} Successfully    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        }
    }
}