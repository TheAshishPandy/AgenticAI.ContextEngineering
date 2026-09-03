// AgenticAI.ContextEngineering.Core/Core/Extensions/EmbeddingRateLimitExtensions.cs
using AgenticAI.ContextEngineering.Core.Embeddings;
using AgenticAI.ContextEngineering.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    /// <summary>
    /// Extension methods for registering rate-limited embedding generation.
    /// </summary>
    public static class EmbeddingRateLimitExtensions
    {
        /// <summary>
        /// Adds rate limiting to the embedding generator registration.
        /// Wraps the existing IEmbeddingGenerator with RateLimitedEmbeddingGenerator.
        /// </summary>
        public static IServiceCollection AddRateLimitedEmbeddingGenerator(
            this IServiceCollection services,
            Action<RateLimitOptions> configure = null)
        {
            var options = new RateLimitOptions();
            configure?.Invoke(options);

            // Find and replace the IEmbeddingGenerator registration
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmbeddingGenerator));
            if (descriptor == null)
            {
                throw new InvalidOperationException(
                    "IEmbeddingGenerator must be registered before AddRateLimitedEmbeddingGenerator can be called");
            }

            // Create wrapper factory
            var wrappedFactory = new Func<IServiceProvider, IEmbeddingGenerator>(provider =>
            {
                // Get the original embedding generator
                IEmbeddingGenerator inner = null;

                if (descriptor.ImplementationInstance != null)
                {
                    inner = (IEmbeddingGenerator)descriptor.ImplementationInstance;
                }
                else if (descriptor.ImplementationFactory != null)
                {
                    inner = (IEmbeddingGenerator)descriptor.ImplementationFactory(provider);
                }
                else if (descriptor.ImplementationType != null)
                {
                    inner = (IEmbeddingGenerator)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);
                }

                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RateLimitedEmbeddingGenerator>>();
                return new RateLimitedEmbeddingGenerator(
                    inner,
                    logger,
                    options.MaxConcurrentRequests,
                    options.MaxRequestsPerMinute);
            });

            // Remove old descriptor and add new one
            services.Remove(descriptor);
            services.Add(new ServiceDescriptor(
                typeof(IEmbeddingGenerator),
                wrappedFactory));

            return services;
        }

        /// <summary>
        /// Adds rate limiting using a factory method with custom configuration.
        /// </summary>
        public static IServiceCollection AddRateLimitedEmbeddingGeneratorFactory(
            this IServiceCollection services,
            int maxConcurrentRequests = 5,
            int maxRequestsPerMinute = 100)
        {
            return services.AddRateLimitedEmbeddingGenerator(options =>
            {
                options.MaxConcurrentRequests = maxConcurrentRequests;
                options.MaxRequestsPerMinute = maxRequestsPerMinute;
            });
        }
    }

    /// <summary>
    /// Configuration options for rate-limited embedding generation.
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// Maximum concurrent embedding requests (default: 5).
        /// </summary>
        public int? MaxConcurrentRequests { get; set; } = 5;

        /// <summary>
        /// Maximum requests per minute (default: 100, typical OpenAI tier-1 limit).
        /// </summary>
        public int? MaxRequestsPerMinute { get; set; } = 100;
    }
}
