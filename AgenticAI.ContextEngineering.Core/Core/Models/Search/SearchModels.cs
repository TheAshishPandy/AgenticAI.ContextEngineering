// Core/Models/SearchModels.cs
using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public enum SearchType
    {
        Lexical = 0,
        Semantic = 1,
        Hybrid = 2,
        HybridWithReranking = 3
    }

    public enum SearchAlgorithm
    {
        BM25 = 0,
        TFIDF = 1,
        Cosine = 2,
        Hybrid = 3
    }

    public class SearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public int TopResults { get; set; } = 10;
        public double MinimumRelevanceScore { get; set; } = 0.0;
        public SearchType SearchType { get; set; } = SearchType.Hybrid;
        public SearchAlgorithm Algorithm { get; set; } = SearchAlgorithm.BM25;
        public Dictionary<string, object>? Filters { get; set; }
        public float[]? QueryVector { get; set; }
        public bool IncludeScoreBreakdown { get; set; } = true;
    }


    public class SearchResponse
    {
        public List<SearchResult> Results { get; set; } = new();
        public bool FromCache { get; set; }

        public long? TotalCount { get; set; }
        public string? SearchMethod { get; set; }
        public TimeSpan?ProcessingTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool HasResults => Results != null && Results.Count > 0;
    }
}