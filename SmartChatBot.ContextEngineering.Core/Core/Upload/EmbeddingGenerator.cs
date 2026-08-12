// Core/Search/EmbeddingGenerator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartChatBot.ContextEngineering.Core.Interfaces;
using SmartChatBot.ContextEngineering.Core.Models;

namespace SmartChatBot.ContextEngineering.Core.Upload
{
    public class EmbeddingGenerator : IEmbeddingGenerator
    {
        private readonly ILogger<EmbeddingGenerator> _logger;
        private readonly SearchOptions _options;
        private readonly IEmbeddingService? _embeddingService;
        private readonly bool _isEnabled;
        private readonly EmbeddingBackend _backend;

        public int Dimensions => _embeddingService?.Dimensions ?? _options.EmbeddingDimensions;
        public bool IsEnabled => _isEnabled;

        public EmbeddingGenerator(
            ILogger<EmbeddingGenerator> logger,
            SearchOptions options,
            IEmbeddingService? embeddingService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _embeddingService = embeddingService;

            // ✅ FIX: Use direct assignment since EmbeddingBackend is non-nullable
            _backend = options.EmbeddingBackend;  // No ?? operator needed

            // Check if embedding service is available
            _isEnabled = embeddingService != null && embeddingService.IsEnabled;

            if (_isEnabled)
            {
                _logger.LogInformation($"EmbeddingGenerator initialized with backend: {_backend}");
            }
            else
            {
                _logger.LogWarning("EmbeddingGenerator running in fallback mode (hash-based embeddings)");
            }
        }

        /// <summary>
        /// Generate embedding for a single text
        /// </summary>
        public async Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<float>();

            try
            {
                if (_isEnabled && _embeddingService != null)
                {
                    return await _embeddingService.GenerateEmbeddingAsync(text, cancellationToken);
                }
                else
                {
                    // Fallback: Generate deterministic hash-based embedding
                    return GenerateHashEmbedding(text);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding, using hash fallback");
                return GenerateHashEmbedding(text);
            }
        }

        /// <summary>
        /// Generate embeddings for multiple texts
        /// </summary>
        public async Task<List<float[]>> GenerateEmbeddingsAsync(
            List<string> texts,
            CancellationToken cancellationToken = default)
        {
            if (texts == null || !texts.Any())
                return new List<float[]>();

            try
            {
                if (_isEnabled && _embeddingService != null)
                {
                    return await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken);
                }
                else
                {
                    // Fallback: Generate deterministic hash-based embeddings
                    return texts.Select(GenerateHashEmbedding).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embeddings, using hash fallback");
                return texts.Select(GenerateHashEmbedding).ToList();
            }
        }

        /// <summary>
        /// Generate deterministic hash-based embedding (fallback when no ML model)
        /// </summary>
        private float[] GenerateHashEmbedding(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new float[384];

            // Use multiple hash functions to create a deterministic embedding
            var embedding = new float[384];
            var chars = text.ToCharArray();

            // Use different hash algorithms for different dimensions
            for (int i = 0; i < embedding.Length; i++)
            {
                // Combine multiple hash values
                int hash1 = GetHashCode(text, i);
                int hash2 = GetHashCode(text, i + 1000);
                int hash3 = GetHashCode(text, i + 2000);

                // Normalize to range [-1, 1]
                float value = (hash1 % 1000) / 1000.0f * 2 - 1;
                value += (hash2 % 500) / 500.0f * 2 - 1;
                value += (hash3 % 200) / 200.0f * 2 - 1;

                // Normalize
                value = Math.Max(-1, Math.Min(1, value / 3));
                embedding[i] = value;
            }

            return embedding;
        }

        /// <summary>
        /// Get hash code for text with seed
        /// </summary>
        private int GetHashCode(string text, int seed)
        {
            unchecked
            {
                int hash = 17 + seed;
                foreach (char c in text)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }

        /// <summary>
        /// Calculate cosine similarity between two embeddings
        /// </summary>
        public double CalculateSimilarity(float[] embedding1, float[] embedding2)
        {
            if (embedding1 == null || embedding2 == null || embedding1.Length == 0 || embedding2.Length == 0)
                return 0;

            if (embedding1.Length != embedding2.Length)
                throw new ArgumentException("Embeddings must have same dimensions");

            double dotProduct = 0;
            double norm1 = 0;
            double norm2 = 0;

            for (int i = 0; i < embedding1.Length; i++)
            {
                dotProduct += embedding1[i] * embedding2[i];
                norm1 += embedding1[i] * embedding1[i];
                norm2 += embedding2[i] * embedding2[i];
            }

            if (norm1 == 0 || norm2 == 0)
                return 0;

            return dotProduct / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
        }

        /// <summary>
        /// Find nearest neighbors using cosine similarity
        /// </summary>
        public List<(float[] Embedding, double Score)> FindNearestNeighbors(
            float[] queryEmbedding,
            List<float[]> candidateEmbeddings,
            int topK = 10)
        {
            if (queryEmbedding == null || candidateEmbeddings == null || !candidateEmbeddings.Any())
                return new List<(float[], double)>();

            var results = new List<(float[] Embedding, double Score)>();

            foreach (var candidate in candidateEmbeddings)
            {
                var score = CalculateSimilarity(queryEmbedding, candidate);
                results.Add((candidate, score));
            }

            return results
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .ToList();
        }

        /// <summary>
        /// Batch process embeddings with progress tracking
        /// </summary>
        public async Task<List<float[]>> BatchGenerateEmbeddingsAsync(
            List<string> texts,
            int batchSize = 10,
            Action<int, int>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<float[]>();
            var total = texts.Count;

            for (int i = 0; i < total; i += batchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var batch = texts.Skip(i).Take(batchSize).ToList();
                var embeddings = await GenerateEmbeddingsAsync(batch, cancellationToken);
                results.AddRange(embeddings);

                onProgress?.Invoke(Math.Min(i + batchSize, total), total);
            }

            return results;
        }

        /// <summary>
        /// Get embedding statistics
        /// </summary>
        public EmbeddingStatistics GetStatistics()
        {
            return new EmbeddingStatistics
            {
                IsEnabled = _isEnabled,
                Backend = _backend.ToString(),
                Dimensions = Dimensions,
                UsingFallback = !_isEnabled
            };
        }
    }

    /// <summary>
    /// Embedding backend types
    /// </summary>

    /// <summary>
    /// Embedding statistics
    /// </summary>
    public class EmbeddingStatistics
    {
        public bool IsEnabled { get; set; }
        public string Backend { get; set; } = string.Empty;
        public int Dimensions { get; set; }
        public bool UsingFallback { get; set; }
    }

    /// <summary>
    /// Interface for embedding service
    /// </summary>
    public interface IEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
        Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);
        int Dimensions { get; }
        bool IsEnabled { get; }
    }
}