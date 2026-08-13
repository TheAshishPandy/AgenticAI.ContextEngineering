// Core/Search/CosineSimilarity.cs
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
        /// Calculate cosine similarity between two vectors
        /// </summary>
        public static double Calculate(float[] vectorA, float[] vectorB)
        {
            if (vectorA == null || vectorB == null || vectorA.Length == 0 || vectorB.Length == 0)
                return 0;

            if (vectorA.Length != vectorB.Length)
                throw new ArgumentException("Vectors must have the same dimensions");

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
                return 0;

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        /// <summary>
        /// Calculate similarity between a vector and multiple vectors
        /// </summary>
        public static List<(int Index, double Score)> CalculateSimilarities(
            float[] queryVector,
            List<float[]> documentVectors)
        {
            var results = new List<(int, double)>();

            for (int i = 0; i < documentVectors.Count; i++)
            {
                var score = Calculate(queryVector, documentVectors[i]);
                results.Add((i, score));
            }

            return results;
        }

        /// <summary>
        /// Normalize a vector to unit length
        /// </summary>
        public static float[] Normalize(float[] vector)
        {
            var norm = Math.Sqrt(vector.Sum(v => v * v));
            if (norm == 0) return vector;

            return vector.Select(v => (float)(v / norm)).ToArray();
        }
    }
}