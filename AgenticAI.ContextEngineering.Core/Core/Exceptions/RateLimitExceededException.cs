// AgenticAI.ContextEngineering.Core/Core/Exceptions/RateLimitExceededException.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when API rate limit is exceeded.
    /// </summary>
    public class RateLimitExceededException : SearchEngineException
    {
        /// <summary>
        /// Gets the number of seconds to wait before retrying.
        /// </summary>
        public int RetryAfterSeconds { get; }

        /// <summary>
        /// Gets the rate limit configured (requests per minute).
        /// </summary>
        public int RateLimitPerMinute { get; }

        /// <summary>
        /// Gets the number of requests already made.
        /// </summary>
        public int RequestsMade { get; }

        /// <summary>
        /// Initializes a new instance of the RateLimitExceededException class.
        /// </summary>
        public RateLimitExceededException(int retryAfterSeconds)
            : base($"Rate limit exceeded. Retry after {retryAfterSeconds} seconds")
        {
            RetryAfterSeconds = retryAfterSeconds;
        }

        /// <summary>
        /// Initializes a new instance with detailed rate limit information.
        /// </summary>
        public RateLimitExceededException(int retryAfterSeconds, int rateLimitPerMinute, int requestsMade)
            : base($"Rate limit exceeded ({requestsMade} requests made, limit is {rateLimitPerMinute}/min). Retry after {retryAfterSeconds} seconds")
        {
            RetryAfterSeconds = retryAfterSeconds;
            RateLimitPerMinute = rateLimitPerMinute;
            RequestsMade = requestsMade;
        }

        /// <summary>
        /// Initializes a new instance with correlation ID.
        /// </summary>
        public RateLimitExceededException(int retryAfterSeconds, string correlationId)
            : base($"Rate limit exceeded. Retry after {retryAfterSeconds} seconds", correlationId)
        {
            RetryAfterSeconds = retryAfterSeconds;
        }
    }
}
