// Core/Search/SemanticSearch.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartChatBot.ContextEngineering.Core.Models;

namespace SmartChatBot.ContextEngineering.Core.Search
{
    public class SemanticSearch
    {
        private readonly SearchIndex _searchIndex;
        private readonly ILogger<SemanticSearch> _logger;
        private readonly SearchOptions _options;  // ✅ Direct injection

        public SemanticSearch(
            SearchIndex searchIndex,
            ILogger<SemanticSearch> logger,
            SearchOptions options)  // ✅ Direct SearchOptions
        {
            _searchIndex = searchIndex;
            _logger = logger;
            _options = options;
        }

        /// <summary>
        /// Perform semantic (vector) search
        /// </summary>
        public async Task<SearchResponse> SearchAsync(
            SearchRequest request,
            float[] queryVector,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var results = new List<SearchResult>();

            try
            {
                if (queryVector == null || queryVector.Length == 0)
                {
                    _logger.LogWarning("Query vector is empty");
                    return new SearchResponse
                    {
                        Results = results,
                        ProcessingTime = stopwatch.Elapsed,
                        SearchMethod = "Semantic (Vector)",
                        Metadata = new Dictionary<string, object> { ["error"] = "Empty query vector" }
                    };
                }

                // Get all documents with embeddings
                var documents = _searchIndex.GetAllDocuments();
                var documentVectors = _searchIndex.GetAllEmbeddings();

                if (!documents.Any() || !documentVectors.Any())
                {
                    return new SearchResponse
                    {
                        Results = results,
                        ProcessingTime = stopwatch.Elapsed,
                        SearchMethod = "Semantic (Vector)",
                        Metadata = new Dictionary<string, object> { ["documents_found"] = 0 }
                    };
                }

                // Calculate cosine similarities
                var similarities = new List<(Document Doc, double Score)>();

                for (int i = 0; i < documents.Count; i++)
                {
                    if (i >= documentVectors.Count) break;

                    var score = CosineSimilarity.Calculate(queryVector, documentVectors[i]);

                    if (score > request.MinimumRelevanceScore || request.MinimumRelevanceScore == 0)
                    {
                        similarities.Add((documents[i], score));
                    }
                }

                // Sort by score descending
                var topResults = similarities
                    .OrderByDescending(s => s.Score)
                    .Take(request.TopResults)
                    .ToList();

                foreach (var (doc, score) in topResults)
                {
                    var searchResult = new SearchResult
                    {
                        Id = doc.Id,
                        Content = doc.Content,
                        Title = doc.Title,
                        Source = doc.Source,
                        Score = score,
                        Metadata = doc.Metadata
                    };

                    if (request.IncludeScoreBreakdown)
                    {
                        searchResult.ScoreBreakdown = new ScoreBreakdown
                        {
                            SemanticScore = score,
                            CombinedScore = score
                        };
                    }

                    results.Add(searchResult);
                }

                stopwatch.Stop();

                return new SearchResponse
                {
                    Results = results,
                    TotalCount = results.Count,
                    SearchMethod = "Semantic (Vector)",
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["documents_scored"] = documents.Count,
                        ["embedding_dimensions"] = queryVector.Length
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Semantic search failed");
                stopwatch.Stop();
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    ProcessingTime = stopwatch.Elapsed,
                    SearchMethod = "Semantic (Vector)",
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    }
                };
            }
        }

        /// <summary>
        /// Perform search with multiple query vectors (for MMR/query expansion)
        /// </summary>
        public async Task<SearchResponse> MultiVectorSearchAsync(
            SearchRequest request,
            List<float[]> queryVectors,
            CancellationToken cancellationToken = default)
        {
            if (queryVectors == null || !queryVectors.Any())
                return await SearchAsync(request, Array.Empty<float>(), cancellationToken);

            var allResults = new List<SearchResult>();
            var processedIds = new HashSet<string>();

            foreach (var vector in queryVectors)
            {
                var response = await SearchAsync(request, vector, cancellationToken);
                foreach (var result in response.Results)
                {
                    if (!processedIds.Contains(result.Id))
                    {
                        allResults.Add(result);
                        processedIds.Add(result.Id);
                    }
                }
            }

            // Aggregate and re-rank
            var groupedResults = allResults
                .GroupBy(r => r.Id)
                .Select(g => new SearchResult
                {
                    Id = g.Key,
                    Content = g.First().Content,
                    Title = g.First().Title,
                    Source = g.First().Source,
                    Score = g.Average(r => r.Score),
                    Metadata = g.First().Metadata
                })
                .OrderByDescending(r => r.Score)
                .Take(request.TopResults)
                .ToList();

            return new SearchResponse
            {
                Results = groupedResults,
                TotalCount = groupedResults.Count,
                SearchMethod = "Semantic (Multi-Vector)",
                ProcessingTime = TimeSpan.Zero
            };
        }
    }
}