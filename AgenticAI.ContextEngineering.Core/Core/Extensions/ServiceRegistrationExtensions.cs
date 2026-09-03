// AgenticAI.ContextEngineering.Core/Extensions/ServiceRegistrationExtensions.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Embeddings;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using AgenticAI.ContextEngineering.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class ServiceRegistrationExtensions
    {
        public static IServiceCollection AddContextEngineering(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            try
            {
                Console.WriteLine("📦 Registering ContextEngineering services...");

                // 1️⃣ Register Core Services
                services.AddSingleton<SearchIndex>();
                Console.WriteLine("  ✅ SearchIndex registered");

                services.Configure<SearchOptions>(options =>
                {
                    options.TopK = configuration.GetValue<int>("Search:DefaultTopK", 10);
                    options.RRF_K = configuration.GetValue<int>("Search:RRF_K", 60);
                    options.EnableReranking = configuration.GetValue<bool>("Search:EnableReranking", false);
                    options.MinimumConfidenceThreshold = configuration.GetValue<double>("Search:MinimumRelevanceScore", 0.3);
                    options.BM25K1 = configuration.GetValue<double>("Search:BM25K1", 1.2);
                    options.BM25B = configuration.GetValue<double>("Search:BM25B", 0.75);
                    options.MaxResults = configuration.GetValue<int>("Search:MaxResults", 100);
                });
                Console.WriteLine("  ✅ SearchOptions configured");

                // 2️⃣ Register Qdrant Client
                services.AddSingleton<IQdrantClient>(sp =>
                {
                    try
                    {
                        var logger = sp.GetService<ILogger<QdrantClient>>();
                        var client = new QdrantClient(configuration, logger!);
                        Console.WriteLine("  ✅ QdrantClient created successfully");
                        return client;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ QdrantClient creation failed: {ex.Message}");
                        Console.WriteLine("  Using MockQdrantClient as fallback...");
                        return new MockQdrantClient();
                    }
                });

                // 3️⃣ Register Caching
                services.AddMemoryCache();
                Console.WriteLine("  ✅ MemoryCache registered");

                // Register Memory KVCache (L1)
                services.AddSingleton<MemoryKVCache>();
                Console.WriteLine("  ✅ MemoryKVCache registered");

                // Register File KVCache (L2) - Optional
                services.AddSingleton<FileKVCache>(sp =>
                {
                    var logger = sp.GetService<ILogger<FileKVCache>>();
                    var cachePath = configuration.GetValue<string>("KVCache:FilePath", "cache.json");
                    return new FileKVCache(cachePath, logger);
                });
                Console.WriteLine("  ✅ FileKVCache registered");

                // Register Distributed KVCache (L3) - Optional
                services.AddSingleton<DistributedKVCache>(sp =>
                {
                    var logger = sp.GetService<ILogger<DistributedKVCache>>();
                    var connectionString = configuration.GetValue<string>("KVCache:ConnectionString", string.Empty);
                    return new DistributedKVCache(connectionString, logger);
                });
                Console.WriteLine("  ✅ DistributedKVCache registered");

                // Register MultiTierKVCache
                services.AddSingleton<IKVCache>(sp =>
                {
                    try
                    {
                        var l1 = sp.GetRequiredService<MemoryKVCache>();
                        var l2 = sp.GetService<FileKVCache>();
                        var l3 = sp.GetService<DistributedKVCache>();
                        var logger = sp.GetRequiredService<ILogger<MultiTierKVCache>>();

                        return new MultiTierKVCache(l1, l2, l3, logger);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ MultiTierKVCache creation failed: {ex.Message}");
                        throw;
                    }
                });
                Console.WriteLine("  ✅ MultiTierKVCache registered as IKVCache");

                // Register Token Cache
                services.AddSingleton<ITokenCache>(sp =>
                {
                    try
                    {
                        var memoryCache = sp.GetRequiredService<IMemoryCache>();
                        var logger = sp.GetService<ILogger<TokenCache>>();
                        return new TokenCache(memoryCache, logger);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ TokenCache creation failed: {ex.Message}");
                        throw;
                    }
                });
                Console.WriteLine("  ✅ ITokenCache registered");

                // ============================================================
                // ✅ 4️⃣ Register Search Services - FIXED: Proper lifetimes
                // ============================================================
                // ✅ Stateless, thread-safe services = Singleton
                services.AddSingleton<LexicalSearch>();
                services.AddSingleton<SemanticSearch>();
                services.AddSingleton<IEmbeddingGenerator, EmbeddingGenerator>();

                // ✅ HybridSearchEngine depends on IAIResponseService (Scoped)
                // So it MUST be Scoped
                services.AddScoped<HybridSearchEngine>();

                // ✅ Search services are Scoped
                services.AddScoped<ISearchService, SearchService>();
                services.AddScoped<CachedSearchService>();

                Console.WriteLine("  ✅ Search services registered (Scoped)");

                // ============================================================
                // ✅ 5️⃣ Register IAIResponseService as Scoped
                // ============================================================
                services.AddScoped<IAIResponseService, AIResponseService>();
                Console.WriteLine("  ✅ IAIResponseService registered (Scoped)");

                // 6️⃣ Register FAQ Service
                services.Configure<FaqOptions>(options =>
                {
                    options.FaqFilePath = configuration.GetValue<string>("FaqService:FaqFilePath") ?? "Data/faq.json";
                    options.DefaultTopK = configuration.GetValue<int>("FaqService:TopK", 10);
                    options.EnableReranking = configuration.GetValue<bool>("FaqService:EnableReranking", true);
                    options.MinimumConfidenceThreshold = configuration.GetValue<double>("FaqService:MinimumConfidenceThreshold", 0.3);
                    options.CacheExpirationHours = configuration.GetValue<int>("FaqService:CacheExpirationHours", 1);
                });

                services.AddSingleton<IFaqService, CachedFaqService>();
                Console.WriteLine("  ✅ FAQ service registered");

                // 7️⃣ Register AI Services
                services.AddAIResponseServiceWithCaching(configuration, "AIResponse");
                Console.WriteLine("  ✅ AI services registered");

                // 8️⃣ Register Token Optimized Service
                services.AddScoped<ITokenOptimizedService, TokenOptimizedService>();
                Console.WriteLine("  ✅ TokenOptimizedService registered");

                Console.WriteLine("✅ All services registered successfully!");
                return services;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Service registration failed: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                throw;
            }
        }
    }

    /// <summary>
    /// Mock Qdrant Client for fallback when Qdrant is not available
    /// </summary>
    public class MockQdrantClient : IQdrantClient
    {
        private readonly Random _random = new();
        private readonly List<SearchResult> _mockResults;

        public MockQdrantClient()
        {
            _mockResults = new List<SearchResult>();
            var mockContents = new[]
            {
                "Artificial intelligence (AI) is the simulation of human intelligence in machines.",
                "Machine learning is a subset of AI that enables systems to learn from data.",
                "Deep learning uses neural networks with multiple layers to learn from data.",
                "Natural language processing (NLP) helps computers understand human language.",
                "Computer vision enables machines to interpret and understand visual information.",
                "Reinforcement learning trains agents to make decisions through trial and error.",
                "Generative AI creates new content like text, images, and music.",
                "Large language models (LLMs) are trained on vast amounts of text data.",
                "Transformers are a neural network architecture used in modern AI models.",
                "Neural networks are computing systems inspired by biological neural networks."
            };

            for (int i = 0; i < mockContents.Length; i++)
            {
                _mockResults.Add(new SearchResult
                {
                    Id = $"mock-{i}",
                    Title = $"Mock Result {i + 1}",
                    Content = mockContents[i % mockContents.Length],
                    Source = "Mock Source",
                    Score = 0.6 + (_random.NextDouble() * 0.35)
                });
            }
        }

        public Task<List<SearchResult>> SearchAsync(
            float[] queryVector,
            int topK = 10,
            float scoreThreshold = 0.3f,
            Dictionary<string, object>? filters = null,
            CancellationToken cancellationToken = default)
        {
            var results = _mockResults
                .Where(r => r.Score >= scoreThreshold)
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .ToList();

            return Task.FromResult(results);
        }

        public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task CreateCollectionAsync(int vectorSize = 1536, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task IndexDocumentAsync(
            string id,
            float[] embedding,
            string content,
            string title = "",
            string source = "",
            Dictionary<string, object>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task IndexDocumentsAsync(
            List<(string Id, float[] Embedding, string Content, string Title, string Source, Dictionary<string, object> Metadata)> documents,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteDocumentAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<long> GetCollectionSizeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult((long)_mockResults.Count);
        }
    }
}