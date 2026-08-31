// AgenticAI.ContextEngineering.Core/Extensions/KVCacheExtensions.cs
using AgenticAI.ContextEngineering.Core.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class KVCacheExtensions
    {
        public static IServiceCollection AddKVCaching(
            this IServiceCollection services,
            Action<KVCacheOptions>? configure = null)
        {
            var options = new KVCacheOptions();
            configure?.Invoke(options);

            // Register Memory Cache
            services.AddMemoryCache();

            // Register concrete cache implementations with their dependencies
            services.TryAddSingleton<MemoryKVCache>();

            // Register FileKVCache with the file path from options
            services.TryAddSingleton<FileKVCache>(sp =>
            {
                var logger = sp.GetService<ILogger<FileKVCache>>();
                return new FileKVCache(options.FilePath, logger);
            });

            // Register DistributedKVCache with connection string from options
            services.TryAddSingleton<DistributedKVCache>(sp =>
            {
                var logger = sp.GetService<ILogger<DistributedKVCache>>();
                return new DistributedKVCache(options.ConnectionString, logger);
            });

            // Register MultiTierKVCache as the main IKVCache
            services.TryAddSingleton<IKVCache>(sp =>
            {
                var l1 = sp.GetRequiredService<MemoryKVCache>();
                var l2 = sp.GetService<FileKVCache>();
                var l3 = sp.GetService<DistributedKVCache>();
                var logger = sp.GetRequiredService<ILogger<MultiTierKVCache>>();

                return new MultiTierKVCache(l1, l2, l3, logger);
            });

            return services;
        }

        /// <summary>
        /// Add KVCaching with configuration from IConfiguration
        /// </summary>
        public static IServiceCollection AddKVCaching(
            this IServiceCollection services,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            string configSection = "KVCache")
        {
            var options = new KVCacheOptions();

            // Bind from configuration
            options.FilePath = configuration[$"{configSection}:FilePath"] ?? "cache.json";
            options.ConnectionString = configuration[$"{configSection}:ConnectionString"] ?? string.Empty;
            options.DefaultExpirationHours = configuration.GetValue<int>($"{configSection}:DefaultExpirationHours", 24);
            options.EnableCompression = configuration.GetValue<bool>($"{configSection}:EnableCompression", true);
            options.MaxSizeMB = configuration.GetValue<int>($"{configSection}:MaxSizeMB", 100);

            return services.AddKVCaching(cfg =>
            {
                cfg.FilePath = options.FilePath;
                cfg.ConnectionString = options.ConnectionString;
                cfg.DefaultExpirationHours = options.DefaultExpirationHours;
                cfg.EnableCompression = options.EnableCompression;
                cfg.MaxSizeMB = options.MaxSizeMB;
            });
        }
    }

    public class KVCacheOptions
    {
        public int DefaultExpirationHours { get; set; } = 24;
        public bool EnableCompression { get; set; } = true;
        public int MaxSizeMB { get; set; } = 100;
        public string FilePath { get; set; } = "cache.json";
        public string ConnectionString { get; set; } = string.Empty;
    }
}