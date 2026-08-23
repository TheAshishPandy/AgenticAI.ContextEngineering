// AgenticAI.ContextEngineering.Core/Interfaces/ISearchService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    /// <summary>
    /// Service for performing hybrid search operations
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        /// Perform search with full request parameters
        /// </summary>
        Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform simple search with just a query string
        /// </summary>
        Task<SearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform lexical (BM25) search
        /// </summary>
        Task<SearchResponse> LexicalSearchAsync(string query, int topResults = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform semantic (cosine similarity) search
        /// </summary>
        Task<SearchResponse> SemanticSearchAsync(string query, int topResults = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get relevant context with conversation history
        /// </summary>
        Task<SearchResponse> GetRelevantContextAsync(
            string query,
            SearchContext? context = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate query variants for better search coverage
        /// </summary>
        Task<List<string>> GenerateQueryVariantsAsync(
            string query,
            SearchContext? context = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Fuse multiple search result lists using RRF
        /// </summary>
        List<SearchResult> FuseResults(List<List<SearchResult>> resultsLists);

        /// <summary>
        /// Clear the search cache
        /// </summary>
        Task ClearSearchCacheAsync();

        /// <summary>
        /// Get cache statistics
        /// </summary>
        Task<Dictionary<string, CacheStatistics>> GetCacheStatsAsync();
    }
}