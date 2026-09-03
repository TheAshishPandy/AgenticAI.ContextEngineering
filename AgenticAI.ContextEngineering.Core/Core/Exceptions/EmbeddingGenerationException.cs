// AgenticAI.ContextEngineering.Core/Core/Exceptions/EmbeddingGenerationException.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when embedding generation fails.
    /// </summary>
    public class EmbeddingGenerationException : SearchEngineException
    {
        /// <summary>
        /// Gets the query that failed to generate embedding for.
        /// </summary>
        public string Query { get; }

        /// <summary>
        /// Gets the length of the query.
        /// </summary>
        public int QueryLength { get; }

        /// <summary>
        /// Initializes a new instance of the EmbeddingGenerationException class.
        /// </summary>
        public EmbeddingGenerationException(string query, Exception innerException)
            : base($"Failed to generate embedding for query of {query?.Length ?? 0} characters", innerException)
        {
            Query = query;
            QueryLength = query?.Length ?? 0;
        }

        /// <summary>
        /// Initializes a new instance of the EmbeddingGenerationException class with a custom message.
        /// </summary>
        public EmbeddingGenerationException(string query, string message, Exception innerException)
            : base($"Embedding generation failed for query: {message}", innerException)
        {
            Query = query;
            QueryLength = query?.Length ?? 0;
        }

        /// <summary>
        /// Initializes a new instance of the EmbeddingGenerationException class with correlation ID.
        /// </summary>
        public EmbeddingGenerationException(string query, Exception innerException, string correlationId)
            : base($"Failed to generate embedding for query of {query?.Length ?? 0} characters", innerException, correlationId)
        {
            Query = query;
            QueryLength = query?.Length ?? 0;
        }
    }
}
