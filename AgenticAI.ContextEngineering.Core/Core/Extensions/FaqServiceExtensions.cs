// AgenticAI.ContextEngineering.Core/Extensions/FaqServiceExtensions.cs
using AgenticAI.ContextEngineering.Core.Embeddings;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using AgenticAI.ContextEngineering.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class FaqServiceExtensions
    {
        public static IServiceCollection AddFaqService(
            this IServiceCollection services,
            Action<SearchOptions>? configureOptions = null)
        {
            var options = new SearchOptions();
            configureOptions?.Invoke(options);
            services.AddSingleton(options);

            // ✅ Register SearchIndex as Singleton
            services.AddSingleton<SearchIndex>();

            // ✅ Register LexicalSearch
            services.AddSingleton<LexicalSearch>();

            // ✅ Register SemanticSearch
            services.AddSingleton<SemanticSearch>();

            // ✅ Register HybridSearchEngine
            services.AddSingleton<HybridSearchEngine>();

            // ✅ Register EmbeddingGenerator
            services.AddSingleton<IEmbeddingGenerator, EmbeddingGenerator>();

            // ✅ Register FAQ Service
            services.AddSingleton<IFaqService, CachedFaqService>();

            return services;
        }

        public static IServiceCollection AddFaqService(
            this IServiceCollection services,
            SearchOptions options)
        {
            services.AddSingleton(options);
            services.AddSingleton<SearchIndex>();
            services.AddSingleton<LexicalSearch>();
            services.AddSingleton<IFaqService, CachedFaqService>();

            return services;
        }
    }
}