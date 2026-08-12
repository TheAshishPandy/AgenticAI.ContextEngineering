// Core/Search/LexicalSearch.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartChatBot.ContextEngineering.Core.Models;

namespace SmartChatBot.ContextEngineering.Core.Search
{
    public class LexicalSearch
    {
        private readonly SearchIndex _searchIndex;
        private readonly ILogger<LexicalSearch> _logger;
        private readonly SearchOptions _options; 

        public LexicalSearch(
            SearchIndex searchIndex,
            ILogger<LexicalSearch> logger,
            SearchOptions options)  // ✅ Direct SearchOptions
        {
            _searchIndex = searchIndex;
            _logger = logger;
            _options = options;
        }

        public async Task<SearchResponse> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var results = new List<SearchResult>();

            try
            {
                // Tokenize query
                var queryTokens = Tokenize(request.Query);
                var documentFrequencies = _searchIndex.GetDocumentFrequencies();

                // Get all documents
                var allDocuments = _searchIndex.GetAllDocuments();

                if (!allDocuments.Any())
                {
                    return new SearchResponse
                    {
                        Results = results,
                        ProcessingTime = stopwatch.Elapsed,
                        SearchMethod = "Lexical (BM25)",
                        Metadata = new Dictionary<string, object> { ["documents_found"] = 0 }
                    };
                }

                // Build BM25 scorer
                var totalDocuments = allDocuments.Count;
                var averageDocumentLength = _searchIndex.GetAverageDocumentLength();

                var bm25Scorer = new BM25Scorer(
                    documentFrequencies,
                    totalDocuments,
                    averageDocumentLength,
                    _options.BM25K1,
                    _options.BM25B);

                // Score each document
                var scoredDocuments = new List<(Document Doc, double Score, Dictionary<string, double> TermScores)>();

                foreach (var doc in allDocuments)
                {
                    var termFrequencies = _searchIndex.GetTermFrequencies(doc.Id);
                    var score = bm25Scorer.Score(termFrequencies, doc.DocumentLength);
                    var termScores = bm25Scorer.GetTermScores(termFrequencies, doc.DocumentLength);

                    if (score > request.MinimumRelevanceScore || request.MinimumRelevanceScore == 0)
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
                                LexicalScore = score,
                                CombinedScore = score,
                                TermScores = termScores
                            };
                        }

                        results.Add(searchResult);
                    }
                }

                results = results.OrderByDescending(r => r.Score).Take(request.TopResults).ToList();

                stopwatch.Stop();

                return new SearchResponse
                {
                    Results = results,
                    TotalCount = results.Count,
                    SearchMethod = "Lexical (BM25)",
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["documents_scored"] = allDocuments.Count,
                        ["query_tokens"] = queryTokens.Count
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lexical search failed for query: {Query}", request.Query);
                stopwatch.Stop();
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    ProcessingTime = stopwatch.Elapsed,
                    SearchMethod = "Lexical (BM25)",
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    }
                };
            }
        }

        public List<string> Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var cleaned = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9\s]", "");
            return cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2)
                .ToList();
        }
    }
}