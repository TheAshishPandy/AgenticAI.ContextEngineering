// Core/Models/SearchOptions.cs
using System;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class SearchOptions
    {
        // BM25 Parameters
        public double BM25K1 { get; set; } = 1.2;
        public double BM25B { get; set; } = 0.75;

        // Hybrid Search Parameters
        public int RRF_K { get; set; } = 60;
        public double SemanticWeight { get; set; } = 0.6;
        public double LexicalWeight { get; set; } = 0.4;

        // General
        public int MaxResults { get; set; } = 100;
        public int TopK { get; set; } = 10;
        public bool EnableReranking { get; set; } = true;
        public double MinimumConfidenceThreshold { get; set; } = 0.3;

        // Indexing
        public bool AutoIndex { get; set; } = true;
        public int BatchSize { get; set; } = 100;

        // ✅ Embedding Settings
        public EmbeddingBackend EmbeddingBackend { get; set; } = EmbeddingBackend.Mock;
        public int EmbeddingDimensions { get; set; } = 384;
        public int EmbeddingBatchSize { get; set; } = 10;
    }

    /// <summary>
    /// Embedding backend types
    /// </summary>
    public enum EmbeddingBackend
    {
        AzureOpenAI,
        SentenceTransformers,
        Mock,
        Hash
    }
}