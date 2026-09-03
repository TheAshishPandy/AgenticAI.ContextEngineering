// AgenticAI.ContextEngineering.Core/Core/Exceptions/ExceptionExtensions.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Exceptions
{
    /// <summary>
    /// Extension methods for exception handling and translation.
    /// </summary>
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Converts common exceptions to SearchEngineException or derived types.
        /// </summary>
        public static SearchEngineException ToSearchEngineException(
            this Exception ex,
            string correlationId = null)
        {
            if (ex == null)
                return new SearchEngineException("An unknown error occurred", correlationId);

            // Already a SearchEngineException, just return it
            if (ex is SearchEngineException searchEx)
                return searchEx;

            // Handle specific exception types
            if (ex is TimeoutException || ex is OperationCanceledException)
            {
                return new OperationTimeoutException(
                    "Unknown operation",
                    30000,
                    ex);
            }

            if (ex is ArgumentException || ex is ArgumentNullException)
            {
                return new InvalidRequestException(
                    ex.Message);
            }

            // Generic conversion
            return new SearchEngineException(
                $"An error occurred: {ex.Message}",
                ex);
        }

        /// <summary>
        /// Gets a user-friendly error message without internal details.
        /// </summary>
        public static string GetUserFriendlyMessage(this SearchEngineException ex)
        {
            return ex switch
            {
                RateLimitExceededException rate => "Too many requests, please try again later",
                DimensionMismatchException => "Vector dimension mismatch - internal error",
                InvalidRequestException invalid => $"Invalid request: {ex.Message}",
                ServiceUnavailableException svc => $"Service temporarily unavailable: {svc.ServiceName}",
                OperationTimeoutException timeout => $"Operation timed out after {timeout.TimeoutMilliseconds}ms",
                EmbeddingGenerationException => "Failed to process your query, please try again",
                _ => "An error occurred processing your request"
            };
        }

        /// <summary>
        /// Checks if exception is transient and can be retried.
        /// </summary>
        public static bool IsTransient(this SearchEngineException ex)
        {
            return ex switch
            {
                RateLimitExceededException => true,
                OperationTimeoutException => true,
                ServiceUnavailableException => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets the suggested retry delay in milliseconds.
        /// </summary>
        public static int GetRetryDelayMs(this SearchEngineException ex)
        {
            return ex switch
            {
                RateLimitExceededException rate => (rate.RetryAfterSeconds > 0 ? rate.RetryAfterSeconds : 60) * 1000,
                OperationTimeoutException => 5000,
                ServiceUnavailableException svc => ((svc.EstimatedRecoverySeconds ?? 10) > 0 ? (svc.EstimatedRecoverySeconds ?? 10) : 60) * 1000,
                _ => 0
            };
        }
    }
}
