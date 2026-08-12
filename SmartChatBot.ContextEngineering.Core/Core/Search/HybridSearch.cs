// Core/Search/HybridSearchEngine.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartChatBot.ContextEngineering.Core.Models;
using SmartChatBot.ContextEngineering.Core.Interfaces;

namespace SmartChatBot.ContextEngineering.Core.Search
{
    public class HybridSearchEngine
    {
        private readonly LexicalSearch _lexicalSearch;
        private readonly SemanticSearch _semanticSearch;
        private readonly SearchIndex _searchIndex;
        private readonly IEmbeddingGenerator _embeddingGenerator;
        private readonly ILogger<HybridSearchEngine> _logger;
        private readonly SearchOptions _options;  // ✅ Direct injection

        public HybridSearchEngine(
            LexicalSearch lexicalSearch,
            SemanticSearch semanticSearch,
            SearchIndex searchIndex,
            IEmbeddingGenerator embeddingGenerator,
            ILogger<HybridSearchEngine> logger,
            SearchOptions options)  // ✅ Direct SearchOptions
        {
            _lexicalSearch = lexicalSearch;
            _semanticSearch = semanticSearch;
            _searchIndex = searchIndex;
            _embeddingGenerator = embeddingGenerator;
            _logger = logger;
            _options = options;
        }

        /// <summary>
        /// Perform hybrid search (lexical + semantic with RRF)
        /// </summary>
        public async Task<SearchResponse> HybridSearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Generate query embedding
                var queryVector = await _embeddingGenerator.GenerateEmbeddingAsync(request.Query, cancellationToken);

                // Perform both searches in parallel
                var lexicalTask = _lexicalSearch.SearchAsync(request, cancellationToken);
                var semanticTask = _semanticSearch.SearchAsync(request, queryVector, cancellationToken);

                await Task.WhenAll(lexicalTask, semanticTask);

                var lexicalResponse = await lexicalTask;
                var semanticResponse = await semanticTask;

                // Fuse results using RRF
                var fusedResults = ReciprocalRankFusion.FuseResults(
                    lexicalResponse.Results,
                    semanticResponse.Results,
                    request.TopResults,
                    _options.RRF_K);

                // Apply reranking if enabled
                if (_options.EnableReranking)
                {
                    fusedResults = await RerankResultsAsync(fusedResults, request.Query, cancellationToken);
                }

                stopwatch.Stop();

                return new SearchResponse
                {
                    Results = fusedResults,
                    TotalCount = fusedResults.Count,
                    SearchMethod = "Hybrid (RRF)",
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["lexical_results"] = lexicalResponse.Results.Count,
                        ["semantic_results"] = semanticResponse.Results.Count,
                        ["fused_results"] = fusedResults.Count,
                        ["lexical_time_ms"] = lexicalResponse.ProcessingTime.TotalMilliseconds,
                        ["semantic_time_ms"] = semanticResponse.ProcessingTime.TotalMilliseconds
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hybrid search failed for query: {Query}", request.Query);
                stopwatch.Stop();
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    ProcessingTime = stopwatch.Elapsed,
                    SearchMethod = "Hybrid (RRF)",
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    }
                };
            }
        }

        /// <summary>
        /// Hybrid search with custom weights
        /// </summary>
        public async Task<SearchResponse> HybridSearchWithWeightsAsync(
            SearchRequest request,
            double lexicalWeight = 0.4,
            double semanticWeight = 0.6,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var queryVector = await _embeddingGenerator.GenerateEmbeddingAsync(request.Query, cancellationToken);

                var lexicalTask = _lexicalSearch.SearchAsync(request, cancellationToken);
                var semanticTask = _semanticSearch.SearchAsync(request, queryVector, cancellationToken);

                await Task.WhenAll(lexicalTask, semanticTask);

                var lexicalResponse = await lexicalTask;
                var semanticResponse = await semanticTask;

                var fusedResults = ReciprocalRankFusion.FuseWithWeights(
                    lexicalResponse.Results,
                    semanticResponse.Results,
                    lexicalWeight,
                    semanticWeight,
                    request.TopResults);

                stopwatch.Stop();

                return new SearchResponse
                {
                    Results = fusedResults,
                    TotalCount = fusedResults.Count,
                    SearchMethod = $"Hybrid (Weights: L={lexicalWeight}, S={semanticWeight})",
                    ProcessingTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Weighted hybrid search failed");
                stopwatch.Stop();
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    }
                };
            }
        }

        /// <summary>
        /// Rerank results using semantic relevance
        /// </summary>
        private async Task<List<SearchResult>> RerankResultsAsync(
            List<SearchResult> results,
            string query,
            CancellationToken cancellationToken)
        {
            if (!results.Any()) return results;

            try
            {
                // Generate embeddings for each result content
                var contents = results.Select(r => r.Content).ToList();
                var embeddings = await _embeddingGenerator.GenerateEmbeddingsAsync(contents, cancellationToken);
                var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(query, cancellationToken);

                // Rerank by semantic similarity
                for (int i = 0; i < results.Count && i < embeddings.Count; i++)
                {
                    var rerankScore = CosineSimilarity.Calculate(queryEmbedding, embeddings[i]);
                    results[i].RerankerScore = rerankScore;

                    if (results[i].ScoreBreakdown != null)
                    {
                        results[i].ScoreBreakdown.RerankerScore = rerankScore;
                        results[i].ScoreBreakdown.CombinedScore = (results[i].Score + rerankScore) / 2;
                    }
                }

                // Re-sort by reranker score
                return results.OrderByDescending(r => r.RerankerScore ?? r.Score).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reranking failed, using original scores");
                return results;
            }
        }
    }
}