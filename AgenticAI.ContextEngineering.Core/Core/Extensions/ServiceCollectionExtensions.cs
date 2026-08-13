// Core/Extensions/ServiceCollectionExtensions.cs
using AgenticAI.ContextEngineering.Core.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AgenticAI.ContextEngineering.Core.Evaluation;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Services;
using AgenticAI.ContextEngineering.Core.Strategies;
using System;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddContextEngineering(
            this IServiceCollection services,
            Action<CompressionOptions>? configureOptions = null)
        {
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            else
            {
                services.Configure<CompressionOptions>(options => { });
            }

            RegisterServices(services);
            return services;
        }

        public static IServiceCollection AddContextEngineering(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSection = "ContextEngineering")
        {
            // ✅ This works with Microsoft.Extensions.Options.ConfigurationExtensions
            services.Configure<CompressionOptions>(configuration.GetSection(configSection));

            RegisterServices(services);
            return services;
        }

        private static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<ITokenEstimator, TokenEstimator>();
            services.AddScoped<IContextCompressor, ContextCompressor>();
            services.AddScoped<ISecretRedactor, SecretRedactor>();
            services.AddScoped<IToolResultPruner, ToolResultPruner>();
            services.AddScoped<IAnchorProtection, AnchorProtection>();
            services.AddScoped<IProgressiveCompression, ProgressiveCompression>();
            services.AddScoped<ContextEngineering.Core.Memory.IAppendOnlyMemory, AppendOnlyMemory>();
            services.AddScoped<ILLMAsJudge, LLMAsJudge>();
        }
    }
}