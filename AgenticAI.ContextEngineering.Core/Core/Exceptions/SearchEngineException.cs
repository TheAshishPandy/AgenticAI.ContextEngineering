// AgenticAI.ContextEngineering.Core/Core/Exceptions/SearchEngineException.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Exceptions
{
    /// <summary>
    /// Base exception for all search engine domain exceptions.
    /// </summary>
    public class SearchEngineException : Exception
    {
        /// <summary>
        /// Gets the timestamp when the exception occurred.
        /// </summary>
        public DateTime OccurredAt { get; }

        /// <summary>
        /// Gets the correlation ID for tracking related operations.
        /// </summary>
        public string CorrelationId { get; }

        /// <summary>
        /// Initializes a new instance of the SearchEngineException class.
        /// </summary>
        public SearchEngineException(string message)
            : base(message)
        {
            OccurredAt = DateTime.UtcNow;
            CorrelationId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Initializes a new instance of the SearchEngineException class with an inner exception.
        /// </summary>
        public SearchEngineException(string message, Exception innerException)
            : base(message, innerException)
        {
            OccurredAt = DateTime.UtcNow;
            CorrelationId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Initializes a new instance of the SearchEngineException class with a correlation ID.
        /// </summary>
        public SearchEngineException(string message, string correlationId)
            : base(message)
        {
            OccurredAt = DateTime.UtcNow;
            CorrelationId = correlationId ?? Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Initializes a new instance of the SearchEngineException class with inner exception and correlation ID.
        /// </summary>
        public SearchEngineException(string message, Exception innerException, string correlationId)
            : base(message, innerException)
        {
            OccurredAt = DateTime.UtcNow;
            CorrelationId = correlationId ?? Guid.NewGuid().ToString();
        }
    }
}
