// AgenticAI.ContextEngineering.Core/Core/Exceptions/InvalidRequestException.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a search request is invalid.
    /// </summary>
    public class InvalidRequestException : SearchEngineException
    {
        /// <summary>
        /// Gets the type of validation that failed.
        /// </summary>
        public string ValidationType { get; }

        /// <summary>
        /// Gets the specific field that failed validation.
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// Gets the invalid value.
        /// </summary>
        public object InvalidValue { get; }

        /// <summary>
        /// Initializes a new instance of the InvalidRequestException class.
        /// </summary>
        public InvalidRequestException(string message)
            : base(message)
        {
            ValidationType = "General";
        }

        /// <summary>
        /// Initializes a new instance with validation details.
        /// </summary>
        public InvalidRequestException(string validationType, string fieldName, object invalidValue)
            : base($"Validation failed for {validationType}: field '{fieldName}' has invalid value")
        {
            ValidationType = validationType;
            FieldName = fieldName;
            InvalidValue = invalidValue;
        }

        /// <summary>
        /// Initializes a new instance with a detailed message.
        /// </summary>
        public InvalidRequestException(string validationType, string fieldName, object invalidValue, string message)
            : base($"{validationType} validation failed for '{fieldName}': {message}")
        {
            ValidationType = validationType;
            FieldName = fieldName;
            InvalidValue = invalidValue;
        }

        /// <summary>
        /// Initializes a new instance with correlation ID.
        /// </summary>
        public InvalidRequestException(string message, string correlationId)
            : base(message, correlationId)
        {
            ValidationType = "General";
        }
    }
}
