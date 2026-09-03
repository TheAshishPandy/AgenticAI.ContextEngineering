// AgenticAI.ContextEngineering.Core/Core/Exceptions/DimensionMismatchException.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when vector dimensions don't match expected size.
    /// </summary>
    public class DimensionMismatchException : SearchEngineException
    {
        /// <summary>
        /// Gets the expected vector dimension.
        /// </summary>
        public int ExpectedDimension { get; }

        /// <summary>
        /// Gets the actual vector dimension.
        /// </summary>
        public int ActualDimension { get; }

        /// <summary>
        /// Gets the identifier of the vector causing the mismatch.
        /// </summary>
        public string VectorId { get; }

        /// <summary>
        /// Initializes a new instance of the DimensionMismatchException class.
        /// </summary>
        public DimensionMismatchException(int expected, int actual)
            : base($"Vector dimension mismatch: expected {expected}D, got {actual}D")
        {
            ExpectedDimension = expected;
            ActualDimension = actual;
        }

        /// <summary>
        /// Initializes a new instance with vector identifier.
        /// </summary>
        public DimensionMismatchException(int expected, int actual, string vectorId)
            : base($"Vector {vectorId} has invalid dimension: expected {expected}D, got {actual}D")
        {
            ExpectedDimension = expected;
            ActualDimension = actual;
            VectorId = vectorId;
        }

        /// <summary>
        /// Initializes a new instance with correlation ID.
        /// </summary>
        public DimensionMismatchException(int expected, int actual, string vectorId, string correlationId)
            : base($"Vector {vectorId} has invalid dimension: expected {expected}D, got {actual}D", correlationId)
        {
            ExpectedDimension = expected;
            ActualDimension = actual;
            VectorId = vectorId;
        }
    }
}
