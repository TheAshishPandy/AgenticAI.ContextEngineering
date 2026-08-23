// AgenticAI.ContextEngineering.Core/Extensions/SearchServiceExtensions.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using AgenticAI.ContextEngineering.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class SearchServiceExtensions
    {
        public static IServiceCollection AddCoreSearchServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register search components
            services.AddSingleton<LexicalSearch>();
            services.AddSingleton<SemanticSearch>();
            services.AddSingleton<HybridSearchEngine>();
            services.AddScoped<ISearchService, SearchService>();
            services.AddScoped<CachedSearchService>();

            // Configure search options
            services.Configure<SearchOptions>(options =>
            {
                configuration.GetSection("Search").Bind(options);
                options.TopK = configuration.GetValue<int>("Search:DefaultTopK", 10);
                options.RRF_K = configuration.GetValue<int>("Search:RRF_K", 60);
                options.EnableReranking = configuration.GetValue<bool>("Search:EnableReranking", false);
                options.MinimumConfidenceThreshold = configuration.GetValue<double>("Search:MinimumRelevanceScore", 0.3);
            });

            return services;
        }
    }
}