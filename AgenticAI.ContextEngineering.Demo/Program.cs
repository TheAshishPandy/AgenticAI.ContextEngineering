// Program.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Extensions;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Demo.Demos;
using AgenticAI.ContextEngineering.Demo.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
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
                var command = args.Length > 0 ? args[0].ToLower() : "all";

                var configuration = BuildConfiguration();

                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(configuration);

                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });

                Console.WriteLine("🔧 Building service container...");
                services.AddContextEngineering(configuration);

                // Register all demo services
                services.AddScoped<KVCacheDemo>();
                services.AddScoped<TokenCacheDemo>();
                services.AddScoped<AIResponseDemo>();
                services.AddScoped<SearchDemo>();
                services.AddScoped<FAQDemo>();
                services.AddScoped<TokenOptimizedDemo>();
                services.AddScoped<StatisticsDemo>();
                services.AddScoped<ClearCacheDemo>();
                services.AddScoped<DemoRunner>();

                Console.WriteLine("🔧 Building service provider...");
                var serviceProvider = services.BuildServiceProvider();
                Console.WriteLine("✅ Service provider built successfully!");

                // Show service status
                await ShowServiceStatusAsync(serviceProvider);

                // Run demos
                var runner = serviceProvider.GetService<DemoRunner>();
                if (runner == null)
                {
                    Console.WriteLine("❌ DemoRunner not available");
                    return;
                }

                await RunCommandAsync(runner, command);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ FATAL ERROR: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }

        static IConfiguration BuildConfiguration()
        {
            var builder = new ConfigurationBuilder();

            var paths = new[]
            {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".."),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory),
                Path.Combine(Directory.GetCurrentDirectory(), "bin", "Debug", "net9.0")
            };

            foreach (var path in paths)
            {
                var configPath = Path.Combine(path, "appsettings.json");
                if (File.Exists(configPath))
                {
                    Console.WriteLine($"✅ Found appsettings.json at: {configPath}");
                    builder.AddJsonFile(configPath, optional: false, reloadOnChange: true);
                    break;
                }
            }

            builder.AddEnvironmentVariables();
            return builder.Build();
        }

        static async Task ShowServiceStatusAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n📋 Service Status:");
            Console.WriteLine(new string('═', 60));

            var serviceTypes = new (Type Type, string Name)[]
            {
                (typeof(IAIResponseService), "AI Response Service"),
                (typeof(ITokenCache), "Token Cache"),
                (typeof(IKVCache), "KV Cache"),
                (typeof(ISearchService), "Search Service"),
                (typeof(IFaqService), "FAQ Service"),
                (typeof(ITokenOptimizedService), "Token Optimized Service"),
                (typeof(IQdrantClient), "Qdrant Client"),
                (typeof(IEmbeddingGenerator), "Embedding Generator")
            };

            foreach (var (type, name) in serviceTypes)
            {
                try
                {
                    var service = serviceProvider.GetService(type);
                    Console.WriteLine($"  {(service != null ? "✅" : "❌")} {name}: {(service != null ? "Registered" : "Not Available")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ {name}: Error - {ex.Message}");
                }
            }

            Console.WriteLine();
        }

        static async Task RunCommandAsync(DemoRunner runner, string command)
        {
            Console.WriteLine($"\n🚀 Running: {command.ToUpper()}");
            Console.WriteLine(new string('═', 60));

            switch (command)
            {
                case "all":
                case "complete":
                    await runner.RunAllDemosAsync();
                    break;

                case "list":
                case "help":
                    runner.ListDemos();
                    ShowHelp();
                    break;

                case "kv":
                    await runner.RunDemoAsync<KVCacheDemo>();
                    break;

                case "token":
                    await runner.RunDemoAsync<TokenCacheDemo>();
                    break;

                case "ai":
                    await runner.RunDemoAsync<AIResponseDemo>();
                    break;

                case "search":
                    await runner.RunDemoAsync<SearchDemo>();
                    break;

                case "faq":
                    await runner.RunDemoAsync<FAQDemo>();
                    break;

                case "optimized":
                    await runner.RunDemoAsync<TokenOptimizedDemo>();
                    break;

                case "stats":
                    await runner.RunDemoAsync<StatisticsDemo>();
                    break;

                case "clear":
                    await runner.RunDemoAsync<ClearCacheDemo>();
                    break;

                // Legacy commands for backward compatibility
                case "kv-test":
                    await runner.RunDemoAsync<KVCacheDemo>();
                    break;

                case "token-cache":
                    await runner.RunDemoAsync<AIResponseDemo>();
                    break;

                case "token-stats":
                    await runner.RunDemoAsync<StatisticsDemo>();
                    break;

                case "token-clear":
                    await runner.RunDemoAsync<ClearCacheDemo>();
                    break;

                case "token-compare":
                    await runner.RunDemoAsync<AIResponseDemo>();
                    break;

                case "token-optimized":
                    await runner.RunDemoAsync<TokenOptimizedDemo>();
                    break;

                default:
                    Console.WriteLine($"❌ Unknown command: {command}");
                    ShowHelp();
                    break;
            }

            Console.WriteLine("\n✅ Demo completed!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void ShowHelp()
        {
            Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════════════╗
║  📖 AVAILABLE DEMO COMMANDS                                          ║
╠═══════════════════════════════════════════════════════════════════════╣
║  all, complete    - Run ALL demos (recommended)                      ║
║  list, help       - Show available demos                            ║
║  kv               - KV Cache operations                              ║
║  token            - Token Cache operations                           ║
║  ai               - AI Response with caching                         ║
║  search           - Search Service                                   ║
║  faq              - FAQ Service                                      ║
║  optimized        - Token Optimized Service                          ║
║  stats            - Cache Statistics                                 ║
║  clear            - Clear all caches                                 ║
╠═══════════════════════════════════════════════════════════════════════╣
║  Legacy Commands (backward compatibility):                           ║
║  kv-test, token-cache, token-stats, token-clear,                    ║
║  token-compare, token-optimized                                     ║
╠═══════════════════════════════════════════════════════════════════════╣
║  Usage: dotnet run -- <command>                                     ║
║  Example: dotnet run -- all                                         ║
╚═══════════════════════════════════════════════════════════════════════╝");
        }
    }
}