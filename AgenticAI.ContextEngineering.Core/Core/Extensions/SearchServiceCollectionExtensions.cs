// Core/Extensions/SearchServiceCollectionExtensions.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgenticAI.ContextEngineering.Core.Embeddings;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using System;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class SearchServiceCollectionExtensions
    {
        public static IServiceCollection AddSearch(
            this IServiceCollection services,
            Action<SearchOptions>? configureOptions = null)
        {
            // ✅ Register SearchOptions as a singleton
            var options = new SearchOptions();
            configureOptions?.Invoke(options);
            services.AddSingleton(options);  // Register as singleton

            // Register core search components
            services.AddSingleton<SearchIndex>();
            services.AddScoped<LexicalSearch>();
            services.AddScoped<SemanticSearch>();
            services.AddScoped<HybridSearchEngine>();

            // Register embedding generator (default to mock)
            services.AddSingleton<IEmbeddingGenerator, MockEmbeddingGenerator>();

            return services;
        }

        public static IServiceCollection AddSearch(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSection = "Search")
        {
            // ✅ Bind configuration to SearchOptions and register as singleton
            var options = new SearchOptions();
            configuration.GetSection(configSection).Bind(options);
            services.AddSingleton(options);

            // Register core search components
            services.AddSingleton<SearchIndex>();
            services.AddScoped<LexicalSearch>();
            services.AddScoped<SemanticSearch>();
            services.AddScoped<HybridSearchEngine>();

            // Register embedding generator (default to mock)
            services.AddSingleton<IEmbeddingGenerator, MockEmbeddingGenerator>();

            return services;
        }

        /// <summary>
        /// Register a custom embedding generator
        /// </summary>
        public static IServiceCollection AddEmbeddingGenerator<T>(
            this IServiceCollection services)
            where T : class, IEmbeddingGenerator
        {
            services.AddSingleton<IEmbeddingGenerator, T>();
            return services;
        }
    }
}