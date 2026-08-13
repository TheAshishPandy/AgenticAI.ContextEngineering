// Core/Models/DocumentModels.cs
using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class DocumentUploadRequest
    {
        public string? FilePath { get; set; }
        public string? Content { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Source { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DocumentType Type { get; set; } = DocumentType.Text;
    }

    public enum DocumentType
    {
        Text,
        PDF,
        Word,
        Excel,
        PowerPoint,
        Image,
        Code,
        Markdown
    }

    public class DocumentUploadResponse
    {
        public string DocumentId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int ChunkCount { get; set; }
        public int TokenCount { get; set; }
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    }

    public class BatchUploadResponse
    {
        public List<DocumentUploadResponse> Results { get; set; } = new();
        public int TotalDocuments { get; set; }
        public int SuccessfulUploads { get; set; }
        public int FailedUploads { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public class SearchQuery
    {
        public string Query { get; set; } = string.Empty;
        public int TopResults { get; set; } = 5;
        public SearchType SearchType { get; set; } = SearchType.Hybrid;
        public Dictionary<string, object>? Filters { get; set; }
        public double MinimumScore { get; set; } = 0.3;
        public bool IncludeEmbeddings { get; set; } = false;
    }

    public class SearchResultWithHighlights : SearchResult
    {
        public List<string> Highlights { get; set; } = new();
        public string? EmbeddingPreview { get; set; }
    }
}