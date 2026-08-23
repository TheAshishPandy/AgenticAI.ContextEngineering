// AgenticAI.ContextEngineering.Core/Models/FaqModels.cs
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgenticAI.ContextEngineering.Core.Models
{
    /// <summary>
    /// FAQ Data Structure from faq.json
    /// </summary>
    public class FaqData
    {
        [JsonPropertyName("faqs")]
        public List<FaqItem> Faqs { get; set; } = new();

        [JsonPropertyName("metadata")]
        public FaqMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Individual FAQ Item
    /// </summary>
    public class FaqItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new();

        [JsonPropertyName("language")]
        public int Language { get; set; } = 1;

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 0;

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// FAQ Metadata
    /// </summary>
    public class FaqMetadata
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("lastUpdated")]
        public string LastUpdated { get; set; } = string.Empty;

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// FAQ Search Request
    /// </summary>
    public class FaqSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public int TopResults { get; set; } = 5;
        public int? Language { get; set; }
        public string? Category { get; set; }
        public double MinimumConfidence { get; set; } = 0.3;
        public bool UseCache { get; set; } = true;
    }

    /// <summary>
    /// FAQ Search Response
    /// </summary>
    public class FaqSearchResponse
    {
        public List<FaqResult> Results { get; set; } = new();
        public int TotalResults { get; set; }
        public bool FromCache { get; set; }
        public long ProcessingTimeMs { get; set; }
        public string? Error { get; set; }
        public bool IsSuccess => string.IsNullOrEmpty(Error);
    }

    /// <summary>
    /// FAQ Result with Score
    /// </summary>
    public class FaqResult : FaqItem
    {
        public double RelevanceScore { get; set; }
        public List<string> MatchedKeywords { get; set; } = new();
        public string? HighlightedQuestion { get; set; }
        public string? HighlightedAnswer { get; set; }
        public ScoreBreakdown? ScoreBreakdown { get; set; }
    }

    /// <summary>
    /// FAQ Statistics
    /// </summary>
    public class FaqStatistics
    {
        public int TotalFaqs { get; set; }
        public Dictionary<string, int> CategoryCounts { get; set; } = new();
        public Dictionary<int, int> LanguageCounts { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public int CacheHitCount { get; set; }
        public int CacheMissCount { get; set; }
        public double CacheHitRate { get; set; }
        public bool IsIndexed { get; set; }
        public int SearchIndexCount { get; set; }
    }
    public class FaqOptions
    {
        public string FaqFilePath { get; set; } = "Data/faq.json";
        public int DefaultTopK { get; set; } = 10;
        public bool EnableReranking { get; set; } = true;
        public double MinimumConfidenceThreshold { get; set; } = 0.3;
        public int CacheExpirationHours { get; set; } = 1;
        public bool ReloadOnChange { get; set; } = true;
    }
}