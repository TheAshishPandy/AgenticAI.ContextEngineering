// Program.cs
using AgenticAI.ContextEngineering.Core.Extensions;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartChatBot.ContextEngineering.Demo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartChatBot.ContextEngineering.Demo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     AgenticAI ContextEngineering - Search Demo           ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            try
            {
                // Build configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
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
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // ✅ Register SearchOptions
                var searchOptions = new SearchOptions();
                configuration.GetSection("Search").Bind(searchOptions);
                services.AddSingleton(searchOptions);

                // ✅ Register Qdrant Client (optional - if not available, use in-memory)
                try
                {
                    services.AddSingleton<IQdrantClient, QdrantClient>();
                    Console.WriteLine("✅ Qdrant Client registered");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Qdrant not available: {ex.Message}");
                    Console.WriteLine("   Using in-memory search instead");
                }

                // ✅ Register SearchIndex
                services.AddSingleton<SearchIndex>();

                // ✅ Register search components
                services.AddScoped<LexicalSearch>();
                services.AddScoped<SemanticSearch>();
                services.AddScoped<HybridSearchEngine>();

                // ✅ Register embedding generator (mock for demo)
                services.AddSingleton<IEmbeddingGenerator, DemoEmbeddingGenerator>();

                // ✅ Register SearchDemo
                services.AddScoped<SearchDemo>();

                var serviceProvider = services.BuildServiceProvider();

                // ✅ Verify services are registered
                var qdrantClient = serviceProvider.GetService<IQdrantClient>();
                var searchIndex = serviceProvider.GetService<SearchIndex>();
                var hybridSearch = serviceProvider.GetService<HybridSearchEngine>();
                var lexicalSearch = serviceProvider.GetService<LexicalSearch>();
                var semanticSearch = serviceProvider.GetService<SemanticSearch>();
                var embeddingGen = serviceProvider.GetService<IEmbeddingGenerator>();

                Console.WriteLine($"✅ IQdrantClient: {(qdrantClient != null ? "Registered" : "Not Available")}");
                Console.WriteLine($"✅ SearchIndex: {(searchIndex != null ? "Registered" : "NULL")}");
                Console.WriteLine($"✅ HybridSearchEngine: {(hybridSearch != null ? "Registered" : "NULL")}");
                Console.WriteLine($"✅ LexicalSearch: {(lexicalSearch != null ? "Registered" : "NULL")}");
                Console.WriteLine($"✅ SemanticSearch: {(semanticSearch != null ? "Registered" : "NULL")}");
                Console.WriteLine($"✅ IEmbeddingGenerator: {(embeddingGen != null ? "Registered" : "NULL")}");
                Console.WriteLine();

                // ✅ Check if Qdrant collection exists
                if (qdrantClient != null)
                {
                    try
                    {
                        var collectionExists = await qdrantClient.CollectionExistsAsync();
                        if (!collectionExists)
                        {
                            Console.WriteLine("📝 Creating Qdrant collection...");
                            await qdrantClient.CreateCollectionAsync(1536);
                            Console.WriteLine("✅ Qdrant collection created");
                        }

                        var size = await qdrantClient.GetCollectionSizeAsync();
                        Console.WriteLine($"📊 Qdrant collection has {size} documents");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Qdrant error: {ex.Message}");
                        Console.WriteLine("   Using in-memory search instead");
                    }
                }

                Console.WriteLine();

                // ✅ Run the demo
                var demo = serviceProvider.GetRequiredService<SearchDemo>();
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

    // ✅ Mock Embedding Generator for Demo
    public class DemoEmbeddingGenerator : IEmbeddingGenerator
    {
        private readonly Random _random = new Random();
        public int Dimensions => 1536;  // ✅ Changed from 384 to 1536
        public bool IsEnabled => true;

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var embedding = new float[1536];  // ✅ Changed from 384 to 1536
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)(_random.NextDouble() * 2 - 1);
            }
            // Normalize
            var norm = MathF.Sqrt(embedding.Sum(x => x * x));
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= norm;
            }
            return Task.FromResult(embedding);
        }

        public Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
        {
            var results = new List<float[]>();
            foreach (var text in texts)
            {
                results.Add(GenerateEmbeddingAsync(text, cancellationToken).Result);
            }
            return Task.FromResult(results);
        }
    }
}