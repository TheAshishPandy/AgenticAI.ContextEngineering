// Core/Search/SemanticSearch.cs
using AgenticAI.ContextEngineering.Core.Exceptions;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            IOptions<SearchOptions> options)  // ✅ Use IOptions
        {
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _qdrantClient = qdrantClient;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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
                // Validate request
                if (request == null)
                    throw new InvalidRequestException("Search", "request", null, "Search request cannot be null");

                // Validate and validate query vector
                if (queryVector == null || queryVector.Length == 0)
                {
                    if (queryVector == null)
                        throw new InvalidRequestException("Search", "queryVector", null, "Query vector cannot be null");

                    return new SearchResponse
                    {
                        Results = results,
                        ProcessingTime = stopwatch.Elapsed,
                        SearchMethod = "Semantic (Vector)",
                        Metadata = new Dictionary<string, object> { ["error"] = "Empty query vector" }
                    };
                }

                // Check expected dimensions
                if (!CosineSimilarity.IsValidDimension(queryVector))
                {
                    var expectedDim = CosineSimilarity.ExpectedDimension ?? 1536;
                    throw new DimensionMismatchException(
                        expectedDim,
                        queryVector.Length,
                        "queryVector");
                }

                // Validate request parameters
                if (request.TopResults <= 0)
                    throw new InvalidRequestException("Search", "TopResults", request.TopResults, "TopResults must be greater than 0");

                if (request.MinimumRelevanceScore < 0 || request.MinimumRelevanceScore > 1)
                    throw new InvalidRequestException("Search", "MinimumRelevanceScore", request.MinimumRelevanceScore, "MinimumRelevanceScore must be between 0 and 1");

                if (_useQdrant && _qdrantClient != null)
                {
                    _logger.LogDebug("Searching via Qdrant...");

                    try
                    {
                        var qdrantResults = await _qdrantClient.SearchAsync(
                            queryVector,
                            request.TopResults,
                            (float)request.MinimumRelevanceScore,
                            request.Filters,
                            cancellationToken);

                        results = qdrantResults ?? new List<SearchResult>();
                        _logger.LogDebug($"Qdrant returned {results.Count} results");
                    }
                    catch (ServiceUnavailableException)
                    {
                        _logger.LogWarning("Qdrant service unavailable, falling back to in-memory search");
                        // Fall through to in-memory search
                    }
                    catch (OperationTimeoutException)
                    {
                        _logger.LogWarning("Qdrant request timed out, falling back to in-memory search");
                        // Fall through to in-memory search
                    }
                }

                // In-memory search (either primary or fallback)
                if (results.Count == 0)
                {
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

                    // Validate document vector dimensions
                    foreach (var docVector in vectorList)
                    {
                        if (docVector != null && docVector.Length != queryVector.Length)
                        {
                            throw new DimensionMismatchException(
                                queryVector.Length,
                                docVector.Length,
                                "documentVector");
                        }
                    }

                    var similarities = new List<(Document Doc, double Score)>();

                    for (int i = 0; i < docList.Count && i < vectorList.Count; i++)
                    {
                        try
                        {
                            var score = CosineSimilarity.Calculate(queryVector, vectorList[i]);
                            if (score > request.MinimumRelevanceScore || request.MinimumRelevanceScore == 0)
                            {
                                similarities.Add((docList[i], score));
                            }
                        }
                        catch (DimensionMismatchException dimEx)
                        {
                            _logger.LogWarning(dimEx, "Skipping document at index {DocIndex} due to dimension mismatch", i);
                            continue;
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
            catch (SearchEngineException searchEx)
            {
                _logger.LogError(searchEx, "Search engine error: {Message}", searchEx.Message);
                stopwatch.Stop();
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    ProcessingTime = stopwatch.Elapsed,
                    SearchMethod = "Semantic (Vector)",
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = searchEx.GetUserFriendlyMessage(),
                        ["correlation_id"] = searchEx.CorrelationId
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Semantic search failed");
                stopwatch.Stop();
                var searchEx = ex.ToSearchEngineException();
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    ProcessingTime = stopwatch.Elapsed,
                    SearchMethod = "Semantic (Vector)",
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["correlation_id"] = searchEx.CorrelationId
                    }
                };
            }
        }
    }
}