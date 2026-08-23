using AgenticAI.ContextEngineering.Core.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class TokenCacheExtensions
    {
        public static IServiceCollection AddTokenCaching(this IServiceCollection services)
        {
            // ✅ Register Token Cache
            services.AddSingleton<ITokenCache, TokenCache>();

            return services;
        }
    }
}