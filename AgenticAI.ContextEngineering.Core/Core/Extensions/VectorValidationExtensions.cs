// AgenticAI.ContextEngineering.Core/Core/Extensions/VectorValidationExtensions.cs
using AgenticAI.ContextEngineering.Core.Constants;
using AgenticAI.ContextEngineering.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    /// <summary>
    /// Extension methods for validating and manipulating vector embeddings.
    /// Provides convenient methods for dimension checking, normalization, and error handling.
    /// </summary>
    public static class VectorValidationExtensions
    {
        /// <summary>
        /// Checks if a vector has the expected embedding dimension.
        /// </summary>
        /// <param name="vector">The vector to validate.</param>
        /// <returns>True if vector dimension matches expected dimension.</returns>
        public static bool IsValidDimension(this float[] vector)
        {
            if (vector == null)
                return false;

            return VectorConfig.MatchesExpectedDimension(vector.Length);
        }

        /// <summary>
        /// Checks if a vector has a valid dimension (between min and max).
        /// </summary>
        /// <param name="vector">The vector to validate.</param>
        /// <returns>True if vector dimension is valid.</returns>
        public static bool IsValidLength(this float[] vector)
        {
            if (vector == null)
                return false;

            return VectorConfig.IsValidDimension(vector.Length);
        }

        /// <summary>
        /// Validates vector dimension and throws if invalid.
        /// </summary>
        /// <param name="vector">The vector to validate.</param>
        /// <param name="vectorName">Optional name for error messages.</param>
        /// <exception cref="DimensionMismatchException">When dimension doesn't match expected.</exception>
        /// <exception cref="InvalidRequestException">When vector is null or empty.</exception>
        public static void ValidateDimension(this float[] vector, string vectorName = "vector")
        {
            if (vector == null)
                throw new InvalidRequestException(
                    "Validation",
                    vectorName,
                    null,
                    $"{vectorName} cannot be null");

            if (vector.Length == 0)
                throw new InvalidRequestException(
                    "Validation",
                    vectorName,
                    vector.Length,
                    $"{vectorName} cannot be empty");

            if (!VectorConfig.MatchesExpectedDimension(vector.Length))
                throw new DimensionMismatchException(
                    VectorConfig.EmbeddingDimension,
                    vector.Length,
                    vectorName);

            // Validate component values
            for (int i = 0; i < vector.Length; i++)
            {
                if (float.IsNaN(vector[i]))
                    throw new InvalidRequestException(
                        "Format",
                        $"{vectorName}[{i}]",
                        vector[i],
                        $"{vectorName} contains NaN value at index {i}");

                if (float.IsInfinity(vector[i]))
                    throw new InvalidRequestException(
                        "Format",
                        $"{vectorName}[{i}]",
                        vector[i],
                        $"{vectorName} contains Infinity value at index {i}");
            }
        }

        /// <summary>
        /// Gets the dimension of a vector or throws if null.
        /// </summary>
        /// <param name="vector">The vector to check.</param>
        /// <param name="vectorName">Optional name for error messages.</param>
        /// <returns>The dimension (length) of the vector.</returns>
        /// <exception cref="InvalidRequestException">When vector is null.</exception>
        public static int GetDimensionOrThrow(this float[] vector, string vectorName = "vector")
        {
            if (vector == null)
                throw new InvalidRequestException(
                    "Validation",
                    vectorName,
                    null,
                    $"{vectorName} cannot be null");

            return vector.Length;
        }

        /// <summary>
        /// Validates that all vectors in a collection have consistent dimensions.
        /// </summary>
        /// <param name="vectors">Collection of vectors to validate.</param>
        /// <param name="vectorName">Optional name for error messages.</param>
        /// <exception cref="InvalidRequestException">When collection is null or empty.</exception>
        /// <exception cref="DimensionMismatchException">When vectors have inconsistent dimensions.</exception>
        public static void ValidateConsistentDimensions(
            this IEnumerable<float[]> vectors,
            string vectorName = "vectors")
        {
            if (vectors == null)
                throw new InvalidRequestException(
                    "Validation",
                    vectorName,
                    null,
                    $"{vectorName} collection cannot be null");

            var vectorList = vectors.ToList();
            if (vectorList.Count == 0)
                throw new InvalidRequestException(
                    "Validation",
                    vectorName,
                    vectorList.Count,
                    $"{vectorName} collection cannot be empty");

            int? expectedDimension = null;

            for (int i = 0; i < vectorList.Count; i++)
            {
                var vector = vectorList[i];

                if (vector == null || vector.Length == 0)
                {
                    throw new InvalidRequestException(
                        "Format",
                        $"{vectorName}[{i}]",
                        vector?.Length ?? 0,
                        $"{vectorName}[{i}] is null or empty");
                }

                if (!expectedDimension.HasValue)
                {
                    expectedDimension = vector.Length;
                }
                else if (vector.Length != expectedDimension.Value)
                {
                    throw new DimensionMismatchException(
                        expectedDimension.Value,
                        vector.Length,
                        $"{vectorName}[{i}]");
                }
            }
        }

        /// <summary>
        /// Checks if a vector contains only valid numerical values (no NaN or Infinity).
        /// </summary>
        /// <param name="vector">The vector to check.</param>
        /// <returns>True if all values are valid numbers.</returns>
        public static bool HasValidValues(this float[] vector)
        {
            if (vector == null || vector.Length == 0)
                return false;

            return vector.All(v => !float.IsNaN(v) && !float.IsInfinity(v));
        }

        /// <summary>
        /// Checks if a vector is a zero vector (all components are zero or very close to zero).
        /// </summary>
        /// <param name="vector">The vector to check.</param>
        /// <param name="epsilon">Tolerance for zero (default: 1e-6).</param>
        /// <returns>True if all components are near zero.</returns>
        public static bool IsZeroVector(this float[] vector, float epsilon = 1e-6f)
        {
            if (vector == null || vector.Length == 0)
                return true;

            return vector.All(v => Math.Abs(v) < epsilon);
        }

        /// <summary>
        /// Calculates the magnitude (L2 norm) of a vector.
        /// </summary>
        /// <param name="vector">The vector to measure.</param>
        /// <returns>The magnitude of the vector.</returns>
        public static double GetMagnitude(this float[] vector)
        {
            if (vector == null || vector.Length == 0)
                return 0.0;

            double sumOfSquares = 0;
            for (int i = 0; i < vector.Length; i++)
            {
                sumOfSquares += vector[i] * vector[i];
            }

            return Math.Sqrt(sumOfSquares);
        }

        /// <summary>
        /// Normalizes a vector to unit length (L2 normalization).
        /// </summary>
        /// <param name="vector">The vector to normalize.</param>
        /// <returns>Normalized vector (new array), or original if magnitude is zero.</returns>
        public static float[] NormalizeL2(this float[] vector)
        {
            if (vector == null || vector.Length == 0)
                return vector;

            double magnitude = vector.GetMagnitude();
            if (magnitude < VectorConfig.SimilarityEpsilon)
                return (float[])vector.Clone();  // Return copy without modification

            var normalized = new float[vector.Length];
            for (int i = 0; i < vector.Length; i++)
            {
                normalized[i] = (float)(vector[i] / magnitude);
            }

            return normalized;
        }

        /// <summary>
        /// Clamps all vector components to valid range [-1, 1].
        /// </summary>
        /// <param name="vector">The vector to clamp.</param>
        /// <returns>New array with clamped values.</returns>
        public static float[] ClampComponents(this float[] vector)
        {
            if (vector == null || vector.Length == 0)
                return vector;

            var clamped = new float[vector.Length];
            for (int i = 0; i < vector.Length; i++)
            {
                clamped[i] = VectorConfig.ClampComponentValue(vector[i]);
            }

            return clamped;
        }

        /// <summary>
        /// Creates a copy of the vector.
        /// </summary>
        /// <param name="vector">The vector to copy.</param>
        /// <returns>A new array with copied values.</returns>
        public static float[] Copy(this float[] vector)
        {
            if (vector == null)
                return null;

            return (float[])vector.Clone();
        }

        /// <summary>
        /// Gets the Euclidean distance between two vectors.
        /// </summary>
        /// <param name="vector1">First vector.</param>
        /// <param name="vector2">Second vector.</param>
        /// <returns>Euclidean distance between vectors.</returns>
        /// <exception cref="DimensionMismatchException">When vectors have different dimensions.</exception>
        public static double EuclideanDistance(this float[] vector1, float[] vector2)
        {
            if (vector1 == null || vector2 == null)
                throw new InvalidRequestException(
                    "Validation",
                    "vectors",
                    null,
                    "Vectors cannot be null");

            if (vector1.Length != vector2.Length)
                throw new DimensionMismatchException(
                    vector1.Length,
                    vector2.Length,
                    "vector2");

            double sumOfSquaredDifferences = 0;
            for (int i = 0; i < vector1.Length; i++)
            {
                double diff = vector1[i] - vector2[i];
                sumOfSquaredDifferences += diff * diff;
            }

            return Math.Sqrt(sumOfSquaredDifferences);
        }

        /// <summary>
        /// Gets a string representation of vector dimensions and statistics.
        /// Useful for logging and debugging.
        /// </summary>
        /// <param name="vector">The vector to describe.</param>
        /// <returns>Human-readable description of the vector.</returns>
        public static string GetDescription(this float[] vector)
        {
            if (vector == null)
                return "Vector[null]";

            if (vector.Length == 0)
                return "Vector[empty]";

            double magnitude = vector.GetMagnitude();
            float min = vector.Min();
            float max = vector.Max();
            double avg = vector.Average();

            return $"Vector[dim={vector.Length}, mag={magnitude:F4}, min={min:F4}, max={max:F4}, avg={avg:F4}]";
        }
    }
}
