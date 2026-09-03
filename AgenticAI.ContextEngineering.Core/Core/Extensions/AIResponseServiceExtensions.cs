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

            // ✅ Register Base AI Response Service
            services.AddScoped<AIResponseService>();
            Console.WriteLine("  ✅ AIResponseService (Base) registered");

            // ✅ Register Token Cache
            services.AddSingleton<ITokenCache, TokenCache>();
            Console.WriteLine("  ✅ ITokenCache registered");

            // ✅ Register Token Usage Tracker
            services.AddSingleton<ITokenUsageTracker, TokenUsageTracker>();
            Console.WriteLine("  ✅ ITokenUsageTracker registered");

            // ✅ Register KV Cache components - NO CIRCULAR DEPENDENCY
            services.AddMemoryCache();

            // Register concrete cache implementations with their dependencies
            services.AddSingleton<MemoryKVCache>();

            // Register FileKVCache with the required string parameter
            services.AddSingleton<FileKVCache>(sp =>
            {
                var logger = sp.GetService<ILogger<FileKVCache>>();
                var cachePath = configuration["KVCache:FilePath"] ??
                               configuration["Cache:FileCachePath"] ??
                               "cache.json";
                return new FileKVCache(cachePath, logger);
            });

            // Register DistributedKVCache with the required string parameter
            services.AddSingleton<DistributedKVCache>(sp =>
            {
                var logger = sp.GetService<ILogger<DistributedKVCache>>();
                var connectionString = configuration["KVCache:ConnectionString"] ??
                                      configuration["Cache:ConnectionString"] ??
                                      string.Empty;
                return new DistributedKVCache(connectionString, logger);
            });

            // Register MultiTierKVCache as IKVCache using factory
            services.AddSingleton<IKVCache>(sp =>
            {
                var memoryCache = sp.GetRequiredService<MemoryKVCache>();
                var fileCache = sp.GetService<FileKVCache>();
                var distributedCache = sp.GetService<DistributedKVCache>();
                var logger = sp.GetService<ILogger<MultiTierKVCache>>();

                return new MultiTierKVCache(memoryCache, fileCache, distributedCache, logger);
            });
            Console.WriteLine("  ✅ IKVCache registered (MultiTierKVCache)");

            // ✅ Register TokenAwareAIResponseService
            services.AddScoped<TokenAwareAIResponseService>(sp =>
            {
                var baseService = sp.GetRequiredService<AIResponseService>();
                var tokenCache = sp.GetRequiredService<ITokenCache>();
                var tokenTracker = sp.GetRequiredService<ITokenUsageTracker>();
                var kvCache = sp.GetService<IKVCache>();
                var logger = sp.GetService<ILogger<TokenAwareAIResponseService>>();

                return new TokenAwareAIResponseService(
                    baseService,
                    tokenCache,
                    tokenTracker,
                    kvCache,
                    logger
                );
            });
            Console.WriteLine("  ✅ TokenAwareAIResponseService registered");

            // ✅ Register CachedAIResponseService
            services.AddScoped<CachedAIResponseService>(sp =>
            {
                var tokenAware = sp.GetRequiredService<TokenAwareAIResponseService>();
                var kvCache = sp.GetService<IKVCache>();
                var logger = sp.GetService<ILogger<CachedAIResponseService>>();
                return new CachedAIResponseService(tokenAware, kvCache, logger);
            });
            Console.WriteLine("  ✅ CachedAIResponseService registered");

            // ✅ Register IAIResponseService
            services.AddScoped<IAIResponseService>(sp =>
            {
                var cachedService = sp.GetRequiredService<CachedAIResponseService>();
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

        public static IServiceCollection AddTokenUsageTracker(
            this IServiceCollection services)
        {
            services.AddSingleton<ITokenUsageTracker, TokenUsageTracker>();
            return services;
        }

        public static IServiceCollection AddKVCaching(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<KVCacheOptions>? configure = null)
        {
            services.AddMemoryCache();

            // Register concrete cache implementations
            services.AddSingleton<MemoryKVCache>();

            services.AddSingleton<FileKVCache>(sp =>
            {
                var logger = sp.GetService<ILogger<FileKVCache>>();
                var cachePath = configuration["KVCache:FilePath"] ??
                               configuration["Cache:FileCachePath"] ??
                               "cache.json";
                return new FileKVCache(cachePath, logger);
            });

            services.AddSingleton<DistributedKVCache>(sp =>
            {
                var logger = sp.GetService<ILogger<DistributedKVCache>>();
                var connectionString = configuration["KVCache:ConnectionString"] ??
                                      configuration["Cache:ConnectionString"] ??
                                      string.Empty;
                return new DistributedKVCache(connectionString, logger);
            });

            // Register MultiTierKVCache as IKVCache
            services.AddSingleton<IKVCache>(sp =>
            {
                var memoryCache = sp.GetRequiredService<MemoryKVCache>();
                var fileCache = sp.GetService<FileKVCache>();
                var distributedCache = sp.GetService<DistributedKVCache>();
                var logger = sp.GetService<ILogger<MultiTierKVCache>>();

                return new MultiTierKVCache(memoryCache, fileCache, distributedCache, logger);
            });

            if (configure != null)
            {
                services.Configure(configure);
            }

            return services;
        }
    }
}