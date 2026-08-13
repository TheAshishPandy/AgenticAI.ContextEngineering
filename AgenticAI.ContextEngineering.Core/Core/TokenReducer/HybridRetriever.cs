// Core/TokenReducer/HybridRetriever.cs
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.TokenReducer
{
    /// <summary>
    /// Hybrid retrieval combining BM25 and vector search
    /// </summary>
    public class HybridRetriever
    {
        private readonly ILogger<HybridRetriever> _logger;
        private readonly TokenReducerConfig _config;

        public HybridRetriever(ILogger<HybridRetriever> logger, TokenReducerConfig config)
        {
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// Retrieve relevant chunks using hybrid approach
        /// </summary>
        public async Task<List<CodeChunk>> RetrieveAsync(
            string query,
            List<CodeChunk> chunks,
            float[]? queryVector = null,
            CancellationToken cancellationToken = default)
        {
            if (!chunks.Any()) return new List<CodeChunk>();

            // 1. BM25 retrieval
            var lexicalResults = await BM25RetrieveAsync(query, chunks, cancellationToken);

            // 2. Vector retrieval (if vector provided)
            var semanticResults = queryVector != null
                ? await VectorRetrieveAsync(queryVector, chunks, cancellationToken)
                : new List<CodeChunk>();

            // 3. Combine results
            var combined = CombineResults(lexicalResults, semanticResults, chunks);

            _logger.LogDebug($"Retrieved {combined.Count} chunks from {chunks.Count}");
            return combined;
        }

        /// <summary>
        /// BM25 retrieval
        /// </summary>
        private async Task<List<CodeChunk>> BM25RetrieveAsync(
            string query,
            List<CodeChunk> chunks,
            CancellationToken cancellationToken)
        {
            var queryTokens = Tokenize(query);
            var results = new List<(CodeChunk Chunk, double Score)>();

            foreach (var chunk in chunks)
            {
                var chunkTokens = Tokenize(chunk.Content);
                var score = ComputeBM25Score(queryTokens, chunkTokens);
                results.Add((chunk, score));
            }

            var topK = Math.Min(_config.SimilarityThreshold / 10, results.Count);
            return results
                .OrderByDescending(x => x.Score)
                .Take(topK > 0 ? topK : 10)
                .Select(x => x.Chunk)
                .ToList();
        }

        /// <summary>
        /// Vector retrieval using cosine similarity
        /// </summary>
        private async Task<List<CodeChunk>> VectorRetrieveAsync(
            float[] queryVector,
            List<CodeChunk> chunks,
            CancellationToken cancellationToken)
        {
            // In production, use actual embeddings
            // For now, use text similarity as fallback
            var queryText = string.Join(" ", queryVector.Select(v => v.ToString()));
            var results = new List<(CodeChunk Chunk, double Score)>();

            foreach (var chunk in chunks)
            {
                var chunkTokens = Tokenize(chunk.Content);
                var queryTokens = Tokenize(queryText);
                var score = ComputeSimilarity(queryTokens, chunkTokens);
                results.Add((chunk, score));
            }

            return results
                .OrderByDescending(x => x.Score)
                .Take(10)
                .Select(x => x.Chunk)
                .ToList();
        }

        /// <summary>
        /// Combine lexical and semantic results
        /// </summary>
        private List<CodeChunk> CombineResults(
            List<CodeChunk> lexical,
            List<CodeChunk> semantic,
            List<CodeChunk> all)
        {
            if (_config.HybridMode == HybridMode.Fallback)
            {
                return lexical.Any() ? lexical : semantic;
            }
            else if (_config.HybridMode == HybridMode.RRF)
            {
                return CombineWithRRF(lexical, semantic, all);
            }
            else // Weighted
            {
                return CombineWithWeights(lexical, semantic, all);
            }
        }

        /// <summary>
        /// Combine results using RRF
        /// </summary>
        private List<CodeChunk> CombineWithRRF(
            List<CodeChunk> lexical,
            List<CodeChunk> semantic,
            List<CodeChunk> all)
        {
            var scores = new Dictionary<string, double>();
            var k = 60;

            // Lexical ranks
            for (int i = 0; i < lexical.Count; i++)
            {
                scores[lexical[i].Id] = 1.0 / (k + i + 1);
            }

            // Semantic ranks
            for (int i = 0; i < semantic.Count; i++)
            {
                if (scores.ContainsKey(semantic[i].Id))
                {
                    scores[semantic[i].Id] += 1.0 / (k + i + 1);
                }
                else
                {
                    scores[semantic[i].Id] = 1.0 / (k + i + 1);
                }
            }

            var orderedIds = scores.OrderByDescending(x => x.Value)
                .Take(_config.SimilarityThreshold / 10)
                .Select(x => x.Key)
                .ToList();

            return all.Where(c => orderedIds.Contains(c.Id)).ToList();
        }

        /// <summary>
        /// Combine results with weights
        /// </summary>
        private List<CodeChunk> CombineWithWeights(
            List<CodeChunk> lexical,
            List<CodeChunk> semantic,
            List<CodeChunk> all)
        {
            var scores = new Dictionary<string, double>();
            var lexicalWeight = 0.4;
            var semanticWeight = 0.6;

            for (int i = 0; i < lexical.Count; i++)
            {
                var score = (1.0 - (double)i / Math.Max(1, lexical.Count)) * lexicalWeight;
                scores[lexical[i].Id] = score;
            }

            for (int i = 0; i < semantic.Count; i++)
            {
                var score = (1.0 - (double)i / Math.Max(1, semantic.Count)) * semanticWeight;
                if (scores.ContainsKey(semantic[i].Id))
                {
                    scores[semantic[i].Id] += score;
                }
                else
                {
                    scores[semantic[i].Id] = score;
                }
            }

            var orderedIds = scores.OrderByDescending(x => x.Value)
                .Take(_config.SimilarityThreshold / 10)
                .Select(x => x.Key)
                .ToList();

            return all.Where(c => orderedIds.Contains(c.Id)).ToList();
        }

        /// <summary>
        /// Compute BM25 score
        /// </summary>
        private double ComputeBM25Score(List<string> queryTokens, List<string> docTokens)
        {
            var k1 = 1.2;
            var b = 0.75;
            var avgDocLength = 200;
            var docLength = docTokens.Count;

            double score = 0;
            var docFreq = docTokens.GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var term in queryTokens)
            {
                if (!docFreq.TryGetValue(term, out var tf)) continue;

                var idf = Math.Log((100 - 0 + 0.5) / (0 + 0.5) + 1.0);
                var numerator = tf * (k1 + 1);
                var denominator = tf + k1 * (1 - b + b * (docLength / avgDocLength));

                score += idf * (numerator / denominator);
            }

            return score;
        }

        /// <summary>
        /// Compute similarity between token sets
        /// </summary>
        private double ComputeSimilarity(List<string> tokens1, List<string> tokens2)
        {
            if (tokens1.Count == 0 || tokens2.Count == 0) return 0;

            var set1 = new HashSet<string>(tokens1);
            var set2 = new HashSet<string>(tokens2);

            var intersect = set1.Intersect(set2).Count();
            var union = set1.Union(set2).Count();

            return union == 0 ? 0 : (double)intersect / union;
        }

        /// <summary>
        /// Tokenize text
        /// </summary>
        private List<string> Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            var cleaned = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9\s_]", "");
            return cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2)
                .ToList();
        }
    }
}