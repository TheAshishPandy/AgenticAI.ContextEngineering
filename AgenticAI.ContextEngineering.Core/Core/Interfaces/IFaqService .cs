// Core/Interfaces/IFaqService.cs
using AgenticAI.ContextEngineering.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    public interface IFaqService
    {
        /// <summary>
        /// Load and index FAQ documents from a JSON file
        /// </summary>
        Task LoadAndIndexFaqsAsync(string faqFilePath);

        /// <summary>
        /// Search FAQ using lexical search (BM25)
        /// </summary>
        Task<List<SearchResult>> SearchFaqsAsync(
            string query,
            int topResults = 5,
            double minScore = 0.1);

        /// <summary>
        /// Get the best matching FAQ answer
        /// </summary>
        Task<string> GetBestAnswerAsync(string query);

        /// <summary>
        /// Get detailed search results with score breakdown
        /// </summary>
        Task<string> GetDetailedSearchAsync(string query);

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