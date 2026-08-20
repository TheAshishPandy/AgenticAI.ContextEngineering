// Core/Search/HybridSearchEngine.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Search
{
    public class HybridSearchEngine
    {
        private readonly LexicalSearch _lexicalSearch;
        private readonly SemanticSearch _semanticSearch;
        private readonly IEmbeddingGenerator _embeddingGenerator;
        private readonly ILogger<HybridSearchEngine> _logger;
        private readonly SearchOptions _options;

        public HybridSearchEngine(
            LexicalSearch lexicalSearch,
            SemanticSearch semanticSearch,
            IEmbeddingGenerator embeddingGenerator,
            ILogger<HybridSearchEngine> logger,
            SearchOptions options)
        {
            _lexicalSearch = lexicalSearch;
            _semanticSearch = semanticSearch;
            _embeddingGenerator = embeddingGenerator;
            _logger = logger;
            _options = options;
        }

        public async Task<SearchResponse> HybridSearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogDebug($"HybridSearchAsync started for query: '{request.Query}'");

                // ✅ Step 1: Generate query embedding for semantic search
                float[] queryVector = null;
                if (request.QueryVector != null && request.QueryVector.Length > 0)
                {
                    queryVector = request.QueryVector;
                    _logger.LogDebug($"Using provided query vector with {queryVector.Length} dimensions");
                }
                else
                {
                    try
                    {
                        _logger.LogDebug("Generating embedding for query...");
                        queryVector = await _embeddingGenerator.GenerateEmbeddingAsync(request.Query, cancellationToken);
                        _logger.LogDebug($"Generated query vector with {queryVector?.Length ?? 0} dimensions");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate embedding for query");
                        queryVector = null;
                    }
                }

                // ✅ Step 2: Perform lexical search
                var lexicalTask = _lexicalSearch.SearchAsync(request, cancellationToken);

                // ✅ Step 3: Perform semantic search (pass queryVector as separate parameter)
                SearchResponse semanticResponse = null;
                if (queryVector != null && queryVector.Length > 0)
                {
                    try
                    {
                        semanticResponse = await _semanticSearch.SearchAsync(
                            request,           // SearchRequest
                            queryVector,       // float[] queryVector (separate parameter)
                            cancellationToken
                        );
                        _logger.LogDebug($"Semantic search returned {semanticResponse?.Results?.Count ?? 0} results");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Semantic search failed, continuing with lexical only");
                        semanticResponse = null;
                    }
                }
                else
                {
                    _logger.LogWarning("No query vector available for semantic search, using lexical only");
                }

                // ✅ Step 4: Get lexical results
                var lexicalResponse = await lexicalTask;
                _logger.LogDebug($"Lexical search returned {lexicalResponse?.Results?.Count ?? 0} results");

                // ✅ Step 5: Fuse results using RRF
                List<SearchResult> fusedResults;
                var hasSemanticResults = semanticResponse?.Results != null && semanticResponse.Results.Any();
                var hasLexicalResults = lexicalResponse?.Results != null && lexicalResponse.Results.Any();

                if (hasLexicalResults && hasSemanticResults)
                {
                    _logger.LogDebug("Fusing lexical and semantic results using RRF");
                    fusedResults = ReciprocalRankFusion.FuseResults(
                        lexicalResponse.Results,
                        semanticResponse.Results,
                        request.TopResults,
                        _options.RRF_K);
                }
                else if (hasLexicalResults)
                {
                    _logger.LogDebug("Using only lexical results (no semantic results)");
                    fusedResults = lexicalResponse.Results.Take(request.TopResults).ToList();
                }
                else if (hasSemanticResults)
                {
                    _logger.LogDebug("Using only semantic results (no lexical results)");
                    fusedResults = semanticResponse.Results.Take(request.TopResults).ToList();
                }
                else
                {
                    _logger.LogWarning("No results from either lexical or semantic search");
                    fusedResults = new List<SearchResult>();
                }

                // ✅ Step 6: Apply reranking if enabled
                if (_options.EnableReranking && fusedResults.Any())
                {
                    _logger.LogDebug("Applying reranking to results");
                    fusedResults = await RerankResultsAsync(fusedResults, request.Query, cancellationToken);
                }

                stopwatch.Stop();

                var response = new SearchResponse
                {
                    Results = fusedResults,
                    TotalCount = fusedResults.Count,
                    SearchMethod = hasLexicalResults && hasSemanticResults ? "Hybrid (RRF)" :
                                  hasLexicalResults ? "Lexical Only" :
                                  hasSemanticResults ? "Semantic Only" : "No Results",
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["lexical_count"] = lexicalResponse?.Results?.Count ?? 0,
                        ["semantic_count"] = semanticResponse?.Results?.Count ?? 0,
                        ["fused_count"] = fusedResults.Count,
                        ["hybrid_enabled"] = hasLexicalResults && hasSemanticResults,
                        ["query_vector_dimensions"] = queryVector?.Length ?? 0
                    }
                };

                _logger.LogDebug($"Hybrid search completed in {stopwatch.ElapsedMilliseconds}ms with {fusedResults.Count} results");
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error in HybridSearchAsync");
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    SearchMethod = "Hybrid (Error)",
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    }
                };
            }
        }

        private async Task<List<SearchResult>> RerankResultsAsync(
            List<SearchResult> results,
            string query,
            CancellationToken cancellationToken)
        {
            // Simple reranking - keep as is for now
            return await Task.FromResult(results.OrderByDescending(r => r.Score).ToList());
        }
    }
}