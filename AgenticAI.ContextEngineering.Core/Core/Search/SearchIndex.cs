// Core/Search/SearchIndex.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Search
{
    public class SearchIndex
    {
        private readonly ConcurrentDictionary<string, Document> _documents = new();
        private readonly ConcurrentDictionary<string, Dictionary<string, int>> _termFrequencies = new();
        private readonly ConcurrentDictionary<string, int> _documentFrequencies = new();
        private readonly object _lock = new();

        /// <summary>
        /// Index a document
        /// </summary>
        public void IndexDocument(Document document)
        {
            if (document == null || string.IsNullOrEmpty(document.Id))
                throw new ArgumentException("Invalid document");

            lock (_lock)
            {
                // Remove existing document if any
                if (_documents.ContainsKey(document.Id))
                    RemoveDocument(document.Id);

                // Tokenize content
                var tokens = Tokenize(document.Content);

                // Calculate term frequencies
                var termFreq = new Dictionary<string, int>();
                foreach (var token in tokens)
                {
                    termFreq[token] = termFreq.GetValueOrDefault(token, 0) + 1;
                }

                // Store document and term frequencies
                document.TermFrequencies = termFreq;
                _documents[document.Id] = document;
                _termFrequencies[document.Id] = termFreq;

                // Update document frequencies
                foreach (var term in termFreq.Keys)
                {
                    _documentFrequencies[term] = _documentFrequencies.GetValueOrDefault(term, 0) + 1;
                }
            }
        }

        /// <summary>
        /// Index multiple documents
        /// </summary>
        public void IndexDocuments(IEnumerable<Document> documents)
        {
            foreach (var doc in documents)
            {
                IndexDocument(doc);
            }
        }

        /// <summary>
        /// Remove a document
        /// </summary>
        public void RemoveDocument(string documentId)
        {
            lock (_lock)
            {
                if (_documents.TryRemove(documentId, out var doc))
                {
                    // Remove term frequencies
                    _termFrequencies.TryRemove(documentId, out _);

                    // Update document frequencies
                    if (doc.TermFrequencies != null)
                    {
                        foreach (var term in doc.TermFrequencies.Keys)
                        {
                            if (_documentFrequencies.TryGetValue(term, out var count))
                            {
                                if (count <= 1)
                                    _documentFrequencies.TryRemove(term, out _);
                                else
                                    _documentFrequencies[term] = count - 1;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get document by ID
        /// </summary>
        public Document? GetDocument(string id)
        {
            _documents.TryGetValue(id, out var doc);
            return doc;
        }

        /// <summary>
        /// Get all documents
        /// </summary>
        public List<Document> GetAllDocuments()
        {
            return _documents.Values.ToList();
        }

        /// <summary>
        /// Get document count
        /// </summary>
        public int Count => _documents.Count;

        /// <summary>
        /// Get term frequencies for a document
        /// </summary>
        public Dictionary<string, int> GetTermFrequencies(string documentId)
        {
            _termFrequencies.TryGetValue(documentId, out var tf);
            return tf ?? new Dictionary<string, int>();
        }

        /// <summary>
        /// Get document frequencies
        /// </summary>
        public Dictionary<string, int> GetDocumentFrequencies()
        {
            return new Dictionary<string, int>(_documentFrequencies);
        }

        /// <summary>
        /// Get all embeddings
        /// </summary>
        public List<float[]> GetAllEmbeddings()
        {
            return _documents.Values
                .Where(d => d.Embedding != null)
                .Select(d => d.Embedding!)
                .ToList();
        }

        /// <summary>
        /// Get average document length
        /// </summary>
        public double GetAverageDocumentLength()
        {
            if (_documents.IsEmpty) return 0;
            var totalLength = _documents.Values.Sum(d => d.DocumentLength);
            return totalLength / (double)_documents.Count;
        }

        /// <summary>
        /// Clear all indexed documents
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _documents.Clear();
                _termFrequencies.Clear();
                _documentFrequencies.Clear();
            }
        }

        /// <summary>
        /// Tokenize text
        /// </summary>
        private List<string> Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var cleaned = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9\s]", "");
            return cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}