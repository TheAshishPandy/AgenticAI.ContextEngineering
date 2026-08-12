// Core/Extensions/ServiceCollectionExtensions.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartChatBot.ContextEngineering.Core.Evaluation;
using SmartChatBot.ContextEngineering.Core.Interfaces;
using SmartChatBot.ContextEngineering.Core.Memory;
using SmartChatBot.ContextEngineering.Core.Models;
using SmartChatBot.ContextEngineering.Core.Services;
using SmartChatBot.ContextEngineering.Core.Strategies;
using System;

namespace SmartChatBot.ContextEngineering.Core.Extensions
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
            services.AddScoped<IAppendOnlyMemory, AppendOnlyMemory>();
            services.AddScoped<ILLMAsJudge, LLMAsJudge>();
        }
    }
}