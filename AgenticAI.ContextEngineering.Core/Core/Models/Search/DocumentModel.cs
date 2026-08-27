// Core/Models/DocumentModel.cs
using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    /// <summary>
    /// Document model for indexing and searching
    /// </summary>
    public class Document
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty; public string Description { get; set; }
        public string? Source { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
        public float[]? Embedding { get; set; }

        // For BM25
        public int DocumentLength => Content.Length;
        public Dictionary<string, int> TermFrequencies { get; set; } = new();
    }
}