// Core/Services/FaqService.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class FaqService : IFaqService
    {
        private readonly SearchIndex _searchIndex;
        private readonly LexicalSearch _lexicalSearch;
        private readonly ILogger<FaqService> _logger;
        private bool _isInitialized = false;

        public FaqService(
            SearchIndex searchIndex,
            LexicalSearch lexicalSearch,
            ILogger<FaqService> logger)
        {
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _lexicalSearch = lexicalSearch ?? throw new ArgumentNullException(nameof(lexicalSearch));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task LoadAndIndexFaqsAsync(string faqFilePath)
        {
            if (_isInitialized)
            {
                _logger.LogInformation("FAQ already loaded");
                return;
            }

            try
            {
                _logger.LogInformation($"Loading FAQ from: {faqFilePath}");

                var loader = new FaqDocumentLoader(faqFilePath);
                var documents = await loader.LoadDocumentsAsync();

                if (documents == null || documents.Count == 0)
                {
                    _logger.LogWarning("No documents found in FAQ file");
                    return;
                }

                _searchIndex.IndexDocuments(documents);
                _isInitialized = true;

                _logger.LogInformation($"✅ Successfully indexed {documents.Count} FAQ documents");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load and index FAQ");
                throw;
            }
        }

        public async Task<List<SearchResult>> SearchFaqsAsync(
            string query,
            int topResults = 5,
            double minScore = 0.1)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("FAQ not loaded yet");
                return new List<SearchResult>();
            }

            try
            {
                var request = new SearchRequest
                {
                    Query = query,
                    TopResults = topResults,
                    MinimumRelevanceScore = minScore,
                    IncludeScoreBreakdown = true
                };

                var response = await _lexicalSearch.SearchAsync(request);
                return response.Results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching FAQ for query: {query}");
                return new List<SearchResult>();
            }
        }

        public async Task<string> GetBestAnswerAsync(string query)
        {
            var results = await SearchFaqsAsync(query, topResults: 1, minScore: 0.1);

            if (results.Any() && results.First().Score > 0.1)
            {
                var best = results.First();
                return $"{best.Title}\n\n{best.Content}";
            }

            return null;
        }

        public async Task<string> GetDetailedSearchAsync(string query)
        {
            var results = await SearchFaqsAsync(query, topResults: 5, minScore: 0);

            if (!results.Any())
                return "No results found.";

            var output = $"📊 Search Results for: \"{query}\"\n";
            output += new string('=', 50) + "\n\n";

            foreach (var result in results)
            {
                output += $"📌 {result.Title}\n";
                output += $"   Score: {result.Score:F3}\n";
                output += $"   ID: {result.Id}\n";
                if (result.ScoreBreakdown != null)
                {
                    output += $"   Lexical Score: {result.ScoreBreakdown.LexicalScore:F3}\n";
                }
                output += $"   Preview: {result.Content?.Substring(0, Math.Min(100, result.Content?.Length ?? 0))}...\n";
                output += "\n";
            }

            return output;
        }

        public bool IsLoaded => _isInitialized;
        public int DocumentCount => _searchIndex.Count;
    }
}