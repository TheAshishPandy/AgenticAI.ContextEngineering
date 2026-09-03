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
        public static IServiceCollection AddAIResponseServiceWithCaching(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSection = "AIResponse")
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            Console.WriteLine("\n🤖 Registering AI Services...");
            Console.WriteLine(new string('═', 60));

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
            Console.WriteLine("  ✅ AIResponseOptions configured");

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
            Console.WriteLine("  ✅ AzureOpenAIClient registered");

            // ✅ Register Base AI Response Service (NO dependencies on IAIResponseService)
            services.AddScoped<AIResponseService>();
            Console.WriteLine("  ✅ AIResponseService (Base) registered");

            // ✅ Register TokenAwareAIResponseService with explicit dependencies
            // Uses concrete AIResponseService, not IAIResponseService
            services.AddScoped<TokenAwareAIResponseService>(sp =>
            {
                var baseService = sp.GetRequiredService<AIResponseService>();
                var tokenCache = sp.GetRequiredService<ITokenCache>();
                var logger = sp.GetRequiredService<ILogger<TokenAwareAIResponseService>>();
                return new TokenAwareAIResponseService(baseService, tokenCache, logger);
            });
            Console.WriteLine("  ✅ TokenAwareAIResponseService registered");

            // ✅ Register CachedAIResponseService with explicit dependencies
            // Uses concrete TokenAwareAIResponseService, not IAIResponseService
            services.AddScoped<CachedAIResponseService>(sp =>
            {
                var tokenAware = sp.GetRequiredService<TokenAwareAIResponseService>();
                var kvCache = sp.GetRequiredService<IKVCache>();
                var logger = sp.GetRequiredService<ILogger<CachedAIResponseService>>();
                return new CachedAIResponseService(tokenAware, kvCache, logger);
            });
            Console.WriteLine("  ✅ CachedAIResponseService registered");

            // ✅ Register IAIResponseService to resolve to CachedAIResponseService
            services.AddScoped<IAIResponseService>(sp =>
            {
                Console.WriteLine("    🔍 Resolving IAIResponseService...");
                var cachedService = sp.GetRequiredService<CachedAIResponseService>();
                Console.WriteLine("    ✅ IAIResponseService resolved to CachedAIResponseService");
                return cachedService;
            });
            Console.WriteLine("  ✅ IAIResponseService registered as Cached");

            Console.WriteLine(new string('═', 60));
            Console.WriteLine("✅ AI services registration complete!\n");

            return services;
        }

        public static IServiceCollection AddTokenCaching(
            this IServiceCollection services)
        {
            services.AddSingleton<ITokenCache, TokenCache>();
            return services;
        }

        public static IServiceCollection AddKVCaching(
            this IServiceCollection services,
            Action<KVCacheOptions>? configure = null)
        {
            services.AddMemoryCache();
            services.AddSingleton<IKVCache, MultiTierKVCache>();

            if (configure != null)
            {
                services.Configure(configure);
            }

            return services;
        }
    }
}