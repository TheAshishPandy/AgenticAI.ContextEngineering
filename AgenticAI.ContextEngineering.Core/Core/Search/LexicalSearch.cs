// Core/Search/LexicalSearch.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Search
{
    public class LexicalSearch
    {
        private readonly SearchIndex _searchIndex;
        private readonly ILogger<LexicalSearch> _logger;
        private readonly SearchOptions _options;
        private string _faqFilePath;
        private bool _isLoading = false;
        private readonly object _lock = new object();

        public LexicalSearch(
            SearchIndex searchIndex,
            ILogger<LexicalSearch> logger,
            IOptions<SearchOptions> options,  // ✅ Use IOptions
            string faqFilePath = null)
        {
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _faqFilePath = faqFilePath ?? string.Empty;
        }

        public void SetFaqFilePath(string filePath)
        {
            _faqFilePath = filePath;
            _logger.LogInformation($"📂 FAQ file path set in LexicalSearch: {_faqFilePath}");
        }

        private async Task LoadDocumentsFromPathAsync()
        {
            if (string.IsNullOrEmpty(_faqFilePath))
            {
                _logger.LogWarning("⚠️ No FAQ file path configured. Cannot load documents.");
                return;
            }

            if (_isLoading)
            {
                _logger.LogInformation("ℹ️ Documents are already being loaded...");
                return;
            }

            lock (_lock)
            {
                if (_isLoading)
                    return;
                _isLoading = true;
            }

            try
            {
                _logger.LogInformation($"📂 Loading documents from: {_faqFilePath}");

                if (!System.IO.File.Exists(_faqFilePath))
                {
                    _logger.LogError($"❌ FAQ file not found: {_faqFilePath}");
                    lock (_lock) { _isLoading = false; }
                    return;
                }

                var jsonContent = await System.IO.File.ReadAllTextAsync(_faqFilePath);

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
                };

                var faqCollection = System.Text.Json.JsonSerializer.Deserialize<FaqCollection>(jsonContent, options);

                if (faqCollection?.Documents == null || faqCollection.Documents.Count == 0)
                {
                    _logger.LogWarning("⚠️ No documents found in FAQ file");
                    lock (_lock) { _isLoading = false; }
                    return;
                }

                _logger.LogInformation($"📊 Found {faqCollection.Documents.Count} documents in JSON");

                var documents = faqCollection.Documents.Select(d => new Document
                {
                    Id = d.Id ?? Guid.NewGuid().ToString(),
                    Title = d.Title ?? "FAQ",
                    Content = d.Content ?? string.Empty,
                    Source = d.Source ?? "FAQ",
                    Metadata = d.Metadata ?? new Dictionary<string, object>()
                }).ToList();

                _searchIndex.Clear();
                _searchIndex.IndexDocuments(documents);

                _logger.LogInformation($"✅ Successfully indexed {_searchIndex.Count} documents into SearchIndex");
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, "❌ Invalid JSON format in FAQ file");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to load documents from path");
            }
            finally
            {
                lock (_lock) { _isLoading = false; }
            }
        }

        public async Task<SearchResponse> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var results = new List<SearchResult>();

            try
            {
                var allDocuments = _searchIndex.GetAllDocuments();

                if (!allDocuments.Any())
                {
                    _logger.LogWarning("⚠️ SearchIndex is EMPTY! Attempting to load documents...");

                    if (!string.IsNullOrEmpty(_faqFilePath))
                    {
                        _logger.LogInformation($"📂 Loading from path: {_faqFilePath}");
                        await LoadDocumentsFromPathAsync();
                        allDocuments = _searchIndex.GetAllDocuments();
                    }

                    if (!allDocuments.Any())
                    {
                        stopwatch.Stop();
                        return new SearchResponse
                        {
                            Results = results,
                            TotalCount = 0,
                            ProcessingTime = stopwatch.Elapsed,
                            SearchMethod = "Lexical (BM25)",
                            Metadata = new Dictionary<string, object>
                            {
                                ["documents_found"] = 0,
                                ["warning"] = "SearchIndex is empty. Please set FAQ file path.",
                                ["query"] = request.Query,
                                ["file_path"] = _faqFilePath ?? "Not set"
                            }
                        };
                    }
                }

                var queryTokens = Tokenize(request.Query);
                var documentFrequencies = _searchIndex.GetDocumentFrequencies();
                var totalDocuments = allDocuments.Count;
                var averageDocumentLength = _searchIndex.GetAverageDocumentLength();

                var bm25Scorer = new BM25Scorer(
                    documentFrequencies,
                    totalDocuments,
                    averageDocumentLength,
                    _options.BM25K1,
                    _options.BM25B);

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
                        ["query_tokens"] = queryTokens.Count,
                        ["total_documents"] = totalDocuments,
                        ["average_document_length"] = averageDocumentLength,
                        ["loaded_from_path"] = !string.IsNullOrEmpty(_faqFilePath)
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

        private class FaqCollection
        {
            public List<FaqDocument> Documents { get; set; } = new();
        }

        private class FaqDocument
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public Dictionary<string, object> Metadata { get; set; } = new();
        }
    }
}