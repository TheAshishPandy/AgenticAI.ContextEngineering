// Core/Extensions/FaqServiceExtensions.cs
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
            // Register SearchOptions
            var options = new SearchOptions();
            configureOptions?.Invoke(options);
            services.AddSingleton(options);

            // Register core components
            services.AddSingleton<SearchIndex>();
            services.AddScoped<LexicalSearch>();

            // Register FaqService
            services.AddScoped<IFaqService, FaqService>();

            return services;
        }

        public static IServiceCollection AddFaqService(
            this IServiceCollection services,
            SearchOptions options)
        {
            services.AddSingleton(options);
            services.AddSingleton<SearchIndex>();
            services.AddScoped<LexicalSearch>();
            services.AddScoped<IFaqService, FaqService>();

            return services;
        }
    }
}