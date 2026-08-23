// AgenticAI.ContextEngineering.Core/Interfaces/IFaqService.cs
using AgenticAI.ContextEngineering.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    public interface IFaqService
    {
        /// <summary>
        /// Load and index FAQ documents from a JSON file
        /// </summary>
        Task LoadAndIndexFaqsAsync(string faqFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Set the FAQ file path for lazy loading
        /// </summary>
        void SetFaqFilePath(string filePath);

        /// <summary>
        /// Try to load FAQ from a file path (returns true if loaded successfully)
        /// </summary>
        Task<bool> TryLoadAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensure FAQ is loaded (lazy loading) - loads if not already loaded
        /// </summary>
        Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Search FAQ using lexical search (BM25) - Auto-loads if not loaded
        /// </summary>
        Task<List<SearchResult>> SearchFaqsAsync(
            string query,
            int topResults = 5,
            double minScore = 0.1,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the best matching FAQ answer - Auto-loads if not loaded
        /// </summary>
        Task<string> GetBestAnswerAsync(string query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get detailed search results with score breakdown - Auto-loads if not loaded
        /// </summary>
        Task<string> GetDetailedSearchAsync(string query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Advanced search with full request/response
        /// </summary>
        Task<FaqSearchResponse> SearchAsync(FaqSearchRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all FAQs
        /// </summary>
        Task<List<FaqItem>> GetAllFaqsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get FAQ by ID
        /// </summary>
        Task<FaqItem?> GetFaqByIdAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get FAQs by category
        /// </summary>
        Task<List<FaqItem>> GetFaqsByCategoryAsync(string category, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get FAQs by language
        /// </summary>
        Task<List<FaqItem>> GetFaqsByLanguageAsync(int language, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get popular FAQs
        /// </summary>
        Task<List<SearchResult>> GetPopularFaqsAsync(int count = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reload FAQs from file
        /// </summary>
        Task ReloadFaqsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Clear FAQ cache
        /// </summary>
        Task ClearCacheAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get FAQ Statistics
        /// </summary>
        Task<FaqStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if FAQ is loaded
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Get total document count
        /// </summary>
        int DocumentCount { get; }
    }
}