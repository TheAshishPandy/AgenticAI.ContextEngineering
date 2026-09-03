// AgenticAI.ContextEngineering.Core/Core/Exceptions/ServiceUnavailableException.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a required service is temporarily unavailable.
    /// </summary>
    public class ServiceUnavailableException : SearchEngineException
    {
        /// <summary>
        /// Gets the name of the unavailable service.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Gets estimated time to recovery in seconds.
        /// </summary>
        public int? EstimatedRecoverySeconds { get; }

        /// <summary>
        /// Initializes a new instance of the ServiceUnavailableException class.
        /// </summary>
        public ServiceUnavailableException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance with service details.
        /// </summary>
        public ServiceUnavailableException(string serviceName, Exception innerException)
            : base($"Service '{serviceName}' is temporarily unavailable", innerException)
        {
            ServiceName = serviceName;
        }

        /// <summary>
        /// Initializes a new instance with recovery time estimate.
        /// </summary>
        public ServiceUnavailableException(string serviceName, int estimatedRecoverySeconds, Exception innerException)
            : base($"Service '{serviceName}' is temporarily unavailable. Estimated recovery in {estimatedRecoverySeconds} seconds", innerException)
        {
            ServiceName = serviceName;
            EstimatedRecoverySeconds = estimatedRecoverySeconds;
        }

        /// <summary>
        /// Initializes a new instance with correlation ID.
        /// </summary>
        public ServiceUnavailableException(string message, string correlationId)
            : base(message, correlationId)
        {
        }
    }
}
