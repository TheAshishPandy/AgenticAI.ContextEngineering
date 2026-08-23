// AgenticAI.ContextEngineering.Core/Extensions/ServiceRegistrationExtensions.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Embeddings;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using AgenticAI.ContextEngineering.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class ServiceRegistrationExtensions
    {
        public static IServiceCollection AddContextEngineering(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 1️⃣ Register Core Services
            services.AddSingleton<SearchIndex>();

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

            services.AddSingleton<IQdrantClient, QdrantClient>();

            // 2️⃣ Register Caching
            services.AddKVCaching(options =>
            {
                configuration.GetSection("KVCache").Bind(options);
            });
            services.AddTokenCaching();

            // 3️⃣ Register Search Services
            services.AddSingleton<LexicalSearch>();
            services.AddSingleton<SemanticSearch>();
            services.AddSingleton<HybridSearchEngine>();
            services.AddSingleton<IEmbeddingGenerator, MockEmbeddingGenerator>();
            services.AddScoped<ISearchService, SearchService>();
            services.AddScoped<CachedSearchService>();

            // 4️⃣ Register FAQ Service
            services.Configure<FaqOptions>(options =>
            {
                options.FaqFilePath = configuration.GetValue<string>("FaqService:FaqFilePath") ?? "Data/faq.json";
                options.DefaultTopK = configuration.GetValue<int>("FaqService:DefaultTopK", 10);
                options.EnableReranking = configuration.GetValue<bool>("FaqService:EnableReranking", true);
                options.MinimumConfidenceThreshold = configuration.GetValue<double>("FaqService:MinimumConfidenceThreshold", 0.3);
                options.CacheExpirationHours = configuration.GetValue<int>("FaqService:CacheExpirationHours", 1);
            });

            services.AddSingleton<IFaqService, CachedFaqService>();

            // 5️⃣ Register AI Services (Simple - No caching decorators)
            services.AddAIResponseService(configuration, "AIResponse");

            // 6️⃣ Register Token Optimized Service
            services.AddScoped<ITokenOptimizedService, TokenOptimizedService>();

            return services;
        }
    }
}