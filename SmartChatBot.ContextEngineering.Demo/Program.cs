// SmartChatBot.ContextEngineering.Demo/Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartChatBot.ContextEngineering.Core.Embeddings;
using SmartChatBot.ContextEngineering.Core.Extensions;
using SmartChatBot.ContextEngineering.Core.Interfaces;
using SmartChatBot.ContextEngineering.Core.Models;
using SmartChatBot.ContextEngineering.Core.Search;
using System;
using System.Threading.Tasks;

namespace SmartChatBot.ContextEngineering.Demo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // Setup DI
                var services = new ServiceCollection();

                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });

                // ✅ Register SearchOptions directly
                var searchOptions = new SearchOptions
                {
                    TopK = 10,
                    BM25K1 = 1.2,
                    BM25B = 0.75,
                    EnableReranking = true,
                    RRF_K = 60,
                    MinimumConfidenceThreshold = 0.3
                };
                services.AddSingleton(searchOptions);

                // Register search components
                services.AddSingleton<SearchIndex>();
                services.AddScoped<LexicalSearch>();
                services.AddScoped<SemanticSearch>();
                services.AddScoped<HybridSearchEngine>();

                // Register embedding generator
                services.AddSingleton<IEmbeddingGenerator, MockEmbeddingGenerator>();

                // Add SearchDemo
                services.AddScoped<SearchDemo>();

                var serviceProvider = services.BuildServiceProvider();
                var demo = serviceProvider.GetRequiredService<SearchDemo>();

                // Run demo
                await demo.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fatal Error: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}