// Core/Models/DocumentModels.cs
using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class DocumentProcessingResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? DocumentId { get; set; }
        public TimeSpan ProcessingTime { get; set; }

        public ExtractionResult ExtractionResult { get; set; } = new();
        public ChunkingResult ChunkingResult { get; set; } = new();
        public EmbeddingResult EmbeddingResult { get; set; } = new();
        public IndexingResult IndexingResult { get; set; } = new();
    }

    public class ExtractionResult
    {
        public bool Success { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    public class ChunkingResult
    {
        public bool Success { get; set; }
        public List<string> Chunks { get; set; } = new();
        public int ChunkCount { get; set; }
        public string? Error { get; set; }
    }

    public class EmbeddingResult
    {
        public bool Success { get; set; }
        public List<float[]> Embeddings { get; set; } = new();
        public string? Error { get; set; }
    }

    public class IndexingResult
    {
        public bool Success { get; set; }
        public string? DocumentId { get; set; }
        public int IndexedCount { get; set; }
        public string? Error { get; set; }
    }

    public class BatchDocumentProcessingResult
    {
        public List<DocumentProcessingResult> Results { get; set; } = new();
        public int TotalDocuments { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public class DocumentProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
    }
}