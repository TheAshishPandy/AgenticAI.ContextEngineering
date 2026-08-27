// AgenticAI.ContextEngineering.Core/Extensions/KVCacheExtensions.cs
using AgenticAI.ContextEngineering.Core.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

            // Only register if not already registered
            services.TryAddSingleton<MemoryKVCache>();
            services.TryAddSingleton<FileKVCache>();
            services.TryAddSingleton<DistributedKVCache>();

            // Register MultiTier as the main IKVCache with proper constructor
            services.TryAddSingleton<IKVCache>(sp =>
            {
                var l1 = sp.GetRequiredService<MemoryKVCache>();
                var l2 = sp.GetService<FileKVCache>();
                var l3 = sp.GetService<DistributedKVCache>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MultiTierKVCache>>();

                return new MultiTierKVCache(l1, l2, l3, logger);
            });

            return services;
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