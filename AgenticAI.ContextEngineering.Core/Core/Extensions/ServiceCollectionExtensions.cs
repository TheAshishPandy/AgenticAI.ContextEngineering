// Core/Extensions/ServiceCollectionExtensions.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        // ... existing AddContextEngineering methods ...

        /// <summary>
        /// Add AI Response Service with conversation summarization and dynamic data handling
        /// </summary>
        public static IServiceCollection AddAIResponseService(
            this IServiceCollection services,
            Action<AIResponseOptions>? configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            else
            {
                services.Configure<AIResponseOptions>(options => { });
            }

            // Register AI services
            services.AddScoped<IAIResponseService, AIResponseService>();

            return services;
        }

        /// <summary>
        /// Add AI Response Service with configuration binding
        /// </summary>
        public static IServiceCollection AddAIResponseService(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSection = "AIResponse")
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            services.Configure<AIResponseOptions>(configuration.GetSection(configSection));
            services.AddScoped<IAIResponseService, AIResponseService>();

            return services;
        }
    }
}