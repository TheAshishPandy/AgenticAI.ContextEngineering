// AgenticAI.ContextEngineering.Core/Extensions/AIResponseServiceExtensions.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Services;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.ClientModel;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class AIResponseServiceExtensions
    {
        /// <summary>
        /// Add AI Response Service with Azure OpenAI - Simple registration
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

            // ✅ Configure AI Response Options
            services.Configure<AIResponseOptions>(options =>
            {
                options.Endpoint = configuration[$"{configSection}:Endpoint"] ?? configuration["AzureOpenAI:Endpoint"] ?? string.Empty;
                options.ApiKey = configuration[$"{configSection}:ApiKey"] ?? configuration["AzureOpenAI:Key"] ?? string.Empty;
                options.DeploymentName = configuration[$"{configSection}:DeploymentName"] ?? configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4";
                options.Temperature = configuration.GetValue<float>($"{configSection}:Temperature", 0.7f);
                options.MaxTokens = configuration.GetValue<int>($"{configSection}:MaxTokens", 500);
                options.SystemPrompt = configuration[$"{configSection}:SystemPrompt"] ?? "You are a helpful assistant.";
                options.UseCache = configuration.GetValue<bool>($"{configSection}:UseCache", true);
                options.Model = configuration[$"{configSection}:Model"] ?? "gpt-4";
            });

            // ✅ Register AzureOpenAIClient
            services.AddSingleton<AzureOpenAIClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AIResponseOptions>>().Value;

                if (string.IsNullOrEmpty(options.Endpoint) || string.IsNullOrEmpty(options.ApiKey))
                {
                    var logger = sp.GetService<ILogger<AIResponseService>>();
                    logger?.LogWarning("⚠️ Azure OpenAI credentials not configured.");
                    return null!;
                }

                try
                {
                    return new AzureOpenAIClient(
                        new Uri(options.Endpoint),
                        new ApiKeyCredential(options.ApiKey));
                }
                catch (Exception ex)
                {
                    var logger = sp.GetService<ILogger<AIResponseService>>();
                    logger?.LogError(ex, "❌ Failed to create AzureOpenAIClient");
                    return null!;
                }
            });

            // ✅ Register Base AI Response Service
            services.AddScoped<AIResponseService>();

            // ✅ Register IAIResponseService as the Cached version
            services.AddScoped<IAIResponseService>(sp =>
            {
                var baseService = sp.GetRequiredService<AIResponseService>();
                var kvCache = sp.GetRequiredService<IKVCache>();
                var logger = sp.GetRequiredService<ILogger<CachedAIResponseService>>();
                return new CachedAIResponseService(baseService, kvCache, logger);
            });

            return services;
        }

        /// <summary>
        /// Add AI Response Service with Token Caching and KV Caching
        /// </summary>
        public static IServiceCollection AddAIResponseServiceWithCaching(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSection = "AIResponse")
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // ✅ Configure AI Response Options
            services.Configure<AIResponseOptions>(options =>
            {
                options.Endpoint = configuration[$"{configSection}:Endpoint"] ?? configuration["AzureOpenAI:Endpoint"] ?? string.Empty;
                options.ApiKey = configuration[$"{configSection}:ApiKey"] ?? configuration["AzureOpenAI:Key"] ?? string.Empty;
                options.DeploymentName = configuration[$"{configSection}:DeploymentName"] ?? configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4";
                options.Temperature = configuration.GetValue<float>($"{configSection}:Temperature", 0.7f);
                options.MaxTokens = configuration.GetValue<int>($"{configSection}:MaxTokens", 500);
                options.SystemPrompt = configuration[$"{configSection}:SystemPrompt"] ?? "You are a helpful assistant.";
                options.UseCache = configuration.GetValue<bool>($"{configSection}:UseCache", true);
                options.Model = configuration[$"{configSection}:Model"] ?? "gpt-4";
            });

            // ✅ Register AzureOpenAIClient
            services.AddSingleton<AzureOpenAIClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AIResponseOptions>>().Value;

                if (string.IsNullOrEmpty(options.Endpoint) || string.IsNullOrEmpty(options.ApiKey))
                {
                    var logger = sp.GetService<ILogger<AIResponseService>>();
                    logger?.LogWarning("⚠️ Azure OpenAI credentials not configured.");
                    return null!;
                }

                try
                {
                    return new AzureOpenAIClient(
                        new Uri(options.Endpoint),
                        new ApiKeyCredential(options.ApiKey));
                }
                catch (Exception ex)
                {
                    var logger = sp.GetService<ILogger<AIResponseService>>();
                    logger?.LogError(ex, "❌ Failed to create AzureOpenAIClient");
                    return null!;
                }
            });

            // ✅ Register Base AI Response Service
            services.AddScoped<AIResponseService>();

            // ✅ Register Token Aware Service (wraps base)
            services.AddScoped<IAIResponseService>(sp =>
            {
                var baseService = sp.GetRequiredService<AIResponseService>();
                var tokenCache = sp.GetRequiredService<ITokenCache>();
                var logger = sp.GetRequiredService<ILogger<TokenAwareAIResponseService>>();
                return new TokenAwareAIResponseService(baseService, tokenCache, logger);
            });

            // ✅ Register Cached Service (wraps Token Aware)
            services.AddScoped<IAIResponseService>(sp =>
            {
                var inner = sp.GetRequiredService<IAIResponseService>(); // This gets TokenAwareAIResponseService
                var kvCache = sp.GetRequiredService<IKVCache>();
                var logger = sp.GetRequiredService<ILogger<CachedAIResponseService>>();
                return new CachedAIResponseService(inner, kvCache, logger);
            });

            return services;
        }
    }
}