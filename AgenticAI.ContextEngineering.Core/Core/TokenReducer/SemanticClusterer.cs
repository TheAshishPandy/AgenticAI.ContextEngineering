// Core/TokenReducer/SemanticClusterer.cs
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
    /// Groups similar chunks using semantic clustering
    /// </summary>
    public class SemanticClusterer
    {
        private readonly ILogger<SemanticClusterer> _logger;
        private readonly TokenReducerConfig _config;

        public SemanticClusterer(ILogger<SemanticClusterer> logger, TokenReducerConfig config)
        {
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// Cluster chunks by semantic similarity
        /// </summary>
        public async Task<List<List<CodeChunk>>> ClusterAsync(
            List<CodeChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            if (chunks.Count <= 1)
                return new List<List<CodeChunk>> { chunks };

            var clusters = new List<List<CodeChunk>>();
            var processed = new HashSet<string>();

            foreach (var chunk in chunks)
            {
                if (processed.Contains(chunk.Id)) continue;

                var cluster = new List<CodeChunk> { chunk };
                processed.Add(chunk.Id);

                var chunkTokens = Tokenize(chunk.Content);

                foreach (var other in chunks)
                {
                    if (processed.Contains(other.Id)) continue;

                    var otherTokens = Tokenize(other.Content);
                    var similarity = ComputeSimilarity(chunkTokens, otherTokens);

                    if (similarity > 0.3) // Similarity threshold
                    {
                        cluster.Add(other);
                        processed.Add(other.Id);
                    }
                }

                clusters.Add(cluster);
            }

            _logger.LogDebug($"Clustered {chunks.Count} chunks into {clusters.Count} clusters");
            return clusters;
        }

        /// <summary>
        /// Get representative chunk from each cluster
        /// </summary>
        public List<CodeChunk> GetClusterRepresentatives(List<List<CodeChunk>> clusters)
        {
            var representatives = new List<CodeChunk>();

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 1)
                {
                    representatives.Add(cluster[0]);
                    continue;
                }

                // Find the chunk closest to the cluster centroid
                var centroid = ComputeClusterCentroid(cluster);
                var representative = FindClosestToCentroid(cluster, centroid);
                representatives.Add(representative);
            }

            return representatives;
        }

        /// <summary>
        /// Compute cluster centroid
        /// </summary>
        private List<string> ComputeClusterCentroid(List<CodeChunk> cluster)
        {
            var allTokens = cluster
                .SelectMany(c => Tokenize(c.Content))
                .ToList();

            // Get top frequent tokens as centroid
            return allTokens
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList();
        }

        /// <summary>
        /// Find chunk closest to centroid
        /// </summary>
        private CodeChunk FindClosestToCentroid(List<CodeChunk> cluster, List<string> centroid)
        {
            var bestChunk = cluster[0];
            var bestScore = 0.0;

            foreach (var chunk in cluster)
            {
                var chunkTokens = Tokenize(chunk.Content);
                var score = ComputeSimilarity(centroid, chunkTokens);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestChunk = chunk;
                }
            }

            return bestChunk;
        }

        /// <summary>
        /// Reduce clusters to top N chunks
        /// </summary>
        public List<CodeChunk> ReduceClusters(
            List<List<CodeChunk>> clusters,
            int maxChunks = 10)
        {
            var result = new List<CodeChunk>();

            // Sort clusters by size (largest first)
            var sortedClusters = clusters
                .OrderByDescending(c => c.Count)
                .ToList();

            foreach (var cluster in sortedClusters)
            {
                if (result.Count >= maxChunks) break;

                var representative = GetClusterRepresentatives(new List<List<CodeChunk>> { cluster });
                result.AddRange(representative);
            }

            return result;
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

        /// <summary>
        /// Compute Jaccard similarity between token sets
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
    }
}