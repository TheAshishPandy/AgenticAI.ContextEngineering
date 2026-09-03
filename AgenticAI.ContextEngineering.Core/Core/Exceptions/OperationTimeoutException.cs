// AgenticAI.ContextEngineering.Core/Core/Exceptions/OperationTimeoutException.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when an operation exceeds its timeout.
    /// </summary>
    public class OperationTimeoutException : SearchEngineException
    {
        /// <summary>
        /// Gets the operation name that timed out.
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Gets the timeout duration in milliseconds.
        /// </summary>
        public int TimeoutMilliseconds { get; }

        /// <summary>
        /// Gets the actual elapsed time in milliseconds.
        /// </summary>
        public int? ElapsedMilliseconds { get; }

        /// <summary>
        /// Initializes a new instance of the OperationTimeoutException class.
        /// </summary>
        public OperationTimeoutException(string operationName, int timeoutMs)
            : base($"Operation '{operationName}' exceeded timeout of {timeoutMs}ms")
        {
            OperationName = operationName;
            TimeoutMilliseconds = timeoutMs;
        }

        /// <summary>
        /// Initializes a new instance with elapsed time tracking.
        /// </summary>
        public OperationTimeoutException(string operationName, int timeoutMs, int elapsedMs)
            : base($"Operation '{operationName}' exceeded timeout of {timeoutMs}ms (elapsed: {elapsedMs}ms)")
        {
            OperationName = operationName;
            TimeoutMilliseconds = timeoutMs;
            ElapsedMilliseconds = elapsedMs;
        }

        /// <summary>
        /// Initializes a new instance with inner exception.
        /// </summary>
        public OperationTimeoutException(string operationName, int timeoutMs, Exception innerException)
            : base($"Operation '{operationName}' exceeded timeout of {timeoutMs}ms", innerException)
        {
            OperationName = operationName;
            TimeoutMilliseconds = timeoutMs;
        }

        /// <summary>
        /// Initializes a new instance with correlation ID.
        /// </summary>
        public OperationTimeoutException(string operationName, int timeoutMs, string correlationId)
            : base($"Operation '{operationName}' exceeded timeout of {timeoutMs}ms", correlationId)
        {
            OperationName = operationName;
            TimeoutMilliseconds = timeoutMs;
        }
    }
}
