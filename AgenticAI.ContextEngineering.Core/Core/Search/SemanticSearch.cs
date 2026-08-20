// Core/Search/SemanticSearch.cs (Updated with Qdrant)
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Search
{
    public class SemanticSearch
    {
        private readonly SearchIndex _searchIndex;
        private readonly IQdrantClient? _qdrantClient;
        private readonly ILogger<SemanticSearch> _logger;
        private readonly SearchOptions _options;
        private readonly bool _useQdrant;

        public SemanticSearch(
            SearchIndex searchIndex,
            IQdrantClient? qdrantClient,
            ILogger<SemanticSearch> logger,
            SearchOptions options)
        {
            _searchIndex = searchIndex;
            _qdrantClient = qdrantClient;
            _logger = logger;
            _options = options;
            _useQdrant = qdrantClient != null;
        }

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
                    return new SearchResponse
                    {
                        Results = results,
                        ProcessingTime = stopwatch.Elapsed,
                        SearchMethod = "Semantic (Vector)",
                        Metadata = new Dictionary<string, object> { ["error"] = "Empty query vector" }
                    };
                }

                // ✅ If using Qdrant, search via Qdrant
                if (_useQdrant && _qdrantClient != null)
                {
                    _logger.LogDebug("Searching via Qdrant...");

                    var qdrantResults = await _qdrantClient.SearchAsync(
                        queryVector,
                        request.TopResults,
                        (float)request.MinimumRelevanceScore,
                        request.Filters,
                        cancellationToken);

                    results = qdrantResults;

                    _logger.LogDebug($"Qdrant returned {results.Count} results");
                }
                else
                {
                    // ✅ In-memory search (fallback)
                    _logger.LogDebug("Searching in-memory...");

                    var documents = _searchIndex.GetAllDocuments();
                    var documentVectors = _searchIndex.GetAllEmbeddings();

                    if (!documents.Any() || !documentVectors.Any())
                    {
                        return new SearchResponse
                        {
                            Results = results,
                            ProcessingTime = stopwatch.Elapsed,
                            SearchMethod = "Semantic (In-Memory)",
                            Metadata = new Dictionary<string, object> { ["documents_found"] = 0 }
                        };
                    }

                    var docList = documents.ToList();
                    var vectorList = documentVectors.ToList();
                    var similarities = new List<(Document Doc, double Score)>();

                    for (int i = 0; i < docList.Count && i < vectorList.Count; i++)
                    {
                        var score = CosineSimilarity.Calculate(queryVector, vectorList[i]);
                        if (score > request.MinimumRelevanceScore || request.MinimumRelevanceScore == 0)
                        {
                            similarities.Add((docList[i], score));
                        }
                    }

                    var topResults = similarities
                        .OrderByDescending(s => s.Score)
                        .Take(request.TopResults)
                        .ToList();

                    foreach (var (doc, score) in topResults)
                    {
                        results.Add(new SearchResult
                        {
                            Id = doc.Id,
                            Content = doc.Content,
                            Title = doc.Title,
                            Source = doc.Source,
                            Score = score,
                            Metadata = doc.Metadata
                        });
                    }
                }

                stopwatch.Stop();

                return new SearchResponse
                {
                    Results = results,
                    TotalCount = results.Count,
                    SearchMethod = _useQdrant ? "Semantic (Qdrant)" : "Semantic (In-Memory)",
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["results_count"] = results.Count,
                        ["search_method"] = _useQdrant ? "Qdrant" : "In-Memory",
                        ["query_vector_dimensions"] = queryVector.Length
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
                    Metadata = new Dictionary<string, object> { ["error"] = ex.Message }
                };
            }
        }
    }
}