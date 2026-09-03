// AgenticAI.ContextEngineering.Core/Core/Constants/VectorConfig.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Constants
{
    /// <summary>
    /// Configuration and constants specific to vector embeddings.
    /// Centralizes all vector-related configuration to ensure consistency across the application.
    /// </summary>
    public static class VectorConfig
    {
        // ============================================================
        // 📏 DIMENSION CONFIGURATION
        // ============================================================

        /// <summary>
        /// Expected embedding dimension for vectors (must match embedding model output).
        /// Default: 1536 (Azure OpenAI text-embedding-3-small)
        /// Common alternatives:
        ///   - 384 (sentence-transformers/all-MiniLM-L6-v2)
        ///   - 768 (sentence-transformers/all-mpnet-base-v2)
        ///   - 3072 (Azure OpenAI text-embedding-3-large)
        /// </summary>
        public const int EmbeddingDimension = DefaultValues.EmbeddingDimension;

        /// <summary>
        /// Minimum allowed vector dimension (for validation).
        /// </summary>
        public const int MinDimension = 1;

        /// <summary>
        /// Maximum allowed vector dimension (for validation).
        /// </summary>
        public const int MaxDimension = 4096;

        /// <summary>
        /// Tolerance for dimension matching (allows slightly different dimensions with warning).
        /// Set to 0 for strict matching.
        /// </summary>
        public const int DimensionTolerance = 0;

        // ============================================================
        // 📊 VECTOR VALIDATION
        // ============================================================

        /// <summary>
        /// Enable strict dimension validation (reject non-matching dimensions).
        /// If false, mismatched dimensions will be warned but allowed.
        /// </summary>
        public const bool StrictDimensionValidation = true;

        /// <summary>
        /// Normalize vectors to unit length before similarity calculation.
        /// Recommended: true for cosine similarity with embeddings.
        /// </summary>
        public const bool AutoNormalizeVectors = false;

        /// <summary>
        /// Maximum allowed absolute value for any vector component.
        /// Embeddings should be normalized to [-1, 1] range.
        /// </summary>
        public const float MaxComponentValue = 1.0f;

        /// <summary>
        /// Minimum allowed absolute value for any vector component.
        /// </summary>
        public const float MinComponentValue = -1.0f;

        // ============================================================
        // 🔍 SIMILARITY CALCULATION
        // ============================================================

        /// <summary>
        /// Expected range for cosine similarity scores (typically [-1, 1]).
        /// </summary>
        public const double MinSimilarityScore = -1.0;
        public const double MaxSimilarityScore = 1.0;

        /// <summary>
        /// Tolerance for similarity score validation (to account for floating-point errors).
        /// </summary>
        public const double SimilarityScoreTolerance = 0.01;

        /// <summary>
        /// Epsilon value for numerical stability in similarity calculations.
        /// Used to avoid division by zero.
        /// </summary>
        public const double SimilarityEpsilon = 1e-10;

        // ============================================================
        // 💾 VECTOR STORAGE
        // ============================================================

        /// <summary>
        /// Estimated bytes per vector component (using float = 4 bytes).
        /// </summary>
        public const int BytesPerComponent = sizeof(float);

        /// <summary>
        /// Estimated size of a single vector in bytes.
        /// </summary>
        public static int EstimatedVectorSizeBytes => EmbeddingDimension * BytesPerComponent;

        // ============================================================
        // ✅ VALIDATION HELPERS
        // ============================================================

        /// <summary>
        /// Checks if a dimension value is valid.
        /// </summary>
        public static bool IsValidDimension(int dimension)
        {
            return dimension >= MinDimension && dimension <= MaxDimension;
        }

        /// <summary>
        /// Checks if a dimension matches the expected embedding dimension.
        /// </summary>
        public static bool MatchesExpectedDimension(int dimension)
        {
            if (DimensionTolerance == 0)
            {
                return dimension == EmbeddingDimension;
            }

            int tolerance = Math.Abs(DimensionTolerance);
            return dimension >= EmbeddingDimension - tolerance &&
                   dimension <= EmbeddingDimension + tolerance;
        }

        /// <summary>
        /// Checks if a similarity score is valid.
        /// </summary>
        public static bool IsValidSimilarityScore(double score)
        {
            return score >= MinSimilarityScore - SimilarityScoreTolerance &&
                   score <= MaxSimilarityScore + SimilarityScoreTolerance;
        }

        /// <summary>
        /// Clamps a similarity score to valid range.
        /// </summary>
        public static double ClampSimilarityScore(double score)
        {
            return Math.Clamp(score, MinSimilarityScore, MaxSimilarityScore);
        }

        /// <summary>
        /// Clamps a vector component to valid range.
        /// </summary>
        public static float ClampComponentValue(float value)
        {
            return Math.Clamp(value, MinComponentValue, MaxComponentValue);
        }

        // ============================================================
        // 📋 DIAGNOSTICS
        // ============================================================

        /// <summary>
        /// Gets a human-readable summary of vector configuration.
        /// </summary>
        public static string GetConfigurationSummary()
        {
            return $"Vector Configuration: " +
                   $"Dimension={EmbeddingDimension}, " +
                   $"EstimatedSize={EstimatedVectorSizeBytes} bytes, " +
                   $"StrictValidation={StrictDimensionValidation}, " +
                   $"AutoNormalize={AutoNormalizeVectors}";
        }
    }
}
