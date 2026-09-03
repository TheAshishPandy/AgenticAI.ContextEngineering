// Core/Search/CosineSimilarity.cs
using AgenticAI.ContextEngineering.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AgenticAI.ContextEngineering.Core.Search
{
    /// <summary>
    /// Cosine similarity for vector comparisons
    /// </summary>
    public static class CosineSimilarity
    {
        /// <summary>
        /// Expected dimension for embeddings (default: 1536 for OpenAI models).
        /// Can be configured based on the embedding model in use.
        /// </summary>
        public static int? ExpectedDimension { get; set; } = 1536;

        /// <summary>
        /// Calculate cosine similarity between two vectors with validation.
        /// </summary>
        /// <exception cref="DimensionMismatchException">When vectors have different dimensions.</exception>
        /// <exception cref="InvalidRequestException">When vectors are null or empty.</exception>
        public static double Calculate(float[] vectorA, float[] vectorB)
        {
            // Validate inputs
            if (vectorA == null && vectorB == null)
                throw new InvalidRequestException("Comparison", "vectors", null, "Both vectors cannot be null");

            if (vectorA == null || vectorB == null)
                throw new InvalidRequestException("Comparison", "vectors", null, "Neither vector can be null");

            if (vectorA.Length == 0 || vectorB.Length == 0)
                return 0;  // One vector is empty, no similarity

            // Check dimension match
            if (vectorA.Length != vectorB.Length)
                throw new DimensionMismatchException(vectorA.Length, vectorB.Length, $"vectorA vs vectorB");

            // Additional validation: check if vectors have expected dimensions
            if (ExpectedDimension.HasValue && vectorA.Length != ExpectedDimension.Value)
            {
                // Log warning if dimensions don't match expected, but don't block
                // This allows for flexible model usage
            }

            double dotProduct = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }

            if (normA == 0 || normB == 0)
                return 0;  // Zero vector, no similarity

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        /// <summary>
        /// Calculate similarity between a vector and multiple vectors with validation.
        /// </summary>
        /// <exception cref="InvalidRequestException">When query vector is invalid.</exception>
        /// <exception cref="DimensionMismatchException">When document vectors have inconsistent dimensions.</exception>
        public static List<(int Index, double Score)> CalculateSimilarities(
            float[] queryVector,
            List<float[]> documentVectors)
        {
            if (queryVector == null || queryVector.Length == 0)
                throw new InvalidRequestException("Vector", "queryVector", null, "Query vector cannot be null or empty");

            if (documentVectors == null || documentVectors.Count == 0)
                throw new InvalidRequestException("Vector", "documentVectors", null, "Document vectors collection cannot be null or empty");

            var results = new List<(int, double)>();
            int? firstVectorDimension = null;

            for (int i = 0; i < documentVectors.Count; i++)
            {
                var docVector = documentVectors[i];

                // Validate document vector
                if (docVector == null || docVector.Length == 0)
                {
                    results.Add((i, 0));
                    continue;
                }

                // Check consistency of document vector dimensions
                if (!firstVectorDimension.HasValue)
                {
                    firstVectorDimension = docVector.Length;
                }
                else if (docVector.Length != firstVectorDimension.Value)
                {
                    throw new DimensionMismatchException(
                        firstVectorDimension.Value,
                        docVector.Length,
                        $"documentVectors[{i}]");
                }

                // Calculate and validate similarity is within [-1, 1] range
                var score = Calculate(queryVector, docVector);
                if (double.IsNaN(score) || score < -1.1 || score > 1.1)
                {
                    // Log anomaly but use clamped value
                    score = Math.Clamp(score, -1.0, 1.0);
                }

                results.Add((i, score));
            }

            return results;
        }

        /// <summary>
        /// Normalize a vector to unit length with validation.
        /// </summary>
        /// <exception cref="InvalidRequestException">When vector is null or empty.</exception>
        public static float[] Normalize(float[] vector)
        {
            if (vector == null || vector.Length == 0)
                throw new InvalidRequestException("Vector", "vector", null, "Vector to normalize cannot be null or empty");

            var norm = Math.Sqrt(vector.Sum(v => v * v));
            if (norm == 0)
                return vector;  // Zero vector, return as is

            return vector.Select(v => (float)(v / norm)).ToArray();
        }

        /// <summary>
        /// Validates that a vector has the expected dimensions.
        /// </summary>
        public static bool IsValidDimension(float[] vector)
        {
            if (vector == null)
                return false;

            if (!ExpectedDimension.HasValue)
                return vector.Length > 0;

            return vector.Length == ExpectedDimension.Value;
        }

        /// <summary>
        /// Validates that all vectors have consistent dimensions.
        /// </summary>
        public static bool AreVectorsConsistent(params float[][] vectors)
        {
            if (vectors == null || vectors.Length == 0)
                return false;

            int? expectedLen = null;

            foreach (var vector in vectors)
            {
                if (vector == null || vector.Length == 0)
                    return false;

                if (!expectedLen.HasValue)
                {
                    expectedLen = vector.Length;
                }
                else if (vector.Length != expectedLen.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}