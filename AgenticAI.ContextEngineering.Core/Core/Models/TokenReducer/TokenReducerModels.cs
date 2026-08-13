// Core/TokenReducer/TokenReducerModels.cs
using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class TokenReducerConfig
    {
        public int ChunkSizeWords { get; set; } = 220;
        public int MaxTokens { get; set; } = 500;
        public string EmbeddingModel { get; set; } = "jinaai/jina-embeddings-v2-base-code";
        public HybridMode HybridMode { get; set; } = HybridMode.Fallback;
        public bool ASTChunkingEnabled { get; set; } = true;
        public bool TextRankEnabled { get; set; } = true;
        public bool ImportGraphEnabled { get; set; } = true;
        public bool TwoHopExpansionEnabled { get; set; } = true;
        public double CompressionRatio { get; set; } = 0.10;
        public int SimilarityThreshold { get; set; } = 70;
        public int QueryCacheTTL { get; set; } = 3600;
    }

    public enum HybridMode
    {
        Fallback,
        Weighted,
        RRF
    }

    public class ContextPacket
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Query { get; set; } = string.Empty;
        public List<CompressedChunk> Chunks { get; set; } = new();
        public List<string> RelevantSymbols { get; set; } = new();
        public Dictionary<string, string> ImportGraph { get; set; } = new();
        public int OriginalTokenCount { get; set; }
        public int CompressedTokenCount { get; set; }
        public double CompressionRatio { get; set; }
        public string Summary { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class CompressedChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public double RelevanceScore { get; set; }
        public List<string> Symbols { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public class CodeChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public List<string> Symbols { get; set; } = new();
        public List<string> Imports { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public double ComplexityScore { get; set; }
    }

    public class MemoryOptions
    {
        public string StoragePath { get; set; } = "./memory";
        public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromDays(30);
    }
}