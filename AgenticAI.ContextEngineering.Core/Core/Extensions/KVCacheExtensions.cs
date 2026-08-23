// AgenticAI.ContextEngineering.Core/Caching/KVCacheExtensions.cs (Ultra Simple)
using AgenticAI.ContextEngineering.Core.Core.Models.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace AgenticAI.ContextEngineering.Core.Caching
{
    public static class KVCacheExtensions
    {
        public static IServiceCollection AddKVCaching(
            this IServiceCollection services,
            Action<KVCacheOptions>? configure = null)
        {
            var options = new KVCacheOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);

            // ✅ SIMPLE: Just Memory Cache
            services.AddMemoryCache();
            services.AddSingleton<IKVCache, MemoryKVCache>();

            // ✅ If you want File Cache too
            if (options.EnableFileCache)
            {
                services.AddSingleton<IKVCache>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<FileKVCache>>();
                    return new FileKVCache(logger, options.FileCacheDirectory);
                });
            }
            return services;
        }
    }
}