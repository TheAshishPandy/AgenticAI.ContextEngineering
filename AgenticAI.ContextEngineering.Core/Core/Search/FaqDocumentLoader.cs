// Core/Services/FaqDocumentLoader.cs
using AgenticAI.ContextEngineering.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class FaqDocumentLoader
    {
        private readonly string _faqFilePath;

        public FaqDocumentLoader(string faqFilePath)
        {
            _faqFilePath = faqFilePath;
        }

        public async Task<List<Document>> LoadDocumentsAsync()
        {
            if (!File.Exists(_faqFilePath))
            {
                throw new FileNotFoundException($"FAQ file not found: {_faqFilePath}");
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(_faqFilePath);
                var faqCollection = JsonSerializer.Deserialize<FaqCollection>(jsonContent);

                if (faqCollection?.Documents == null || faqCollection.Documents.Count == 0)
                {
                    throw new InvalidOperationException("No documents found in FAQ file");
                }

                var documents = new List<Document>();

                foreach (var faqDoc in faqCollection.Documents)
                {
                    var document = new Document
                    {
                        Id = faqDoc.Id,
                        Title = faqDoc.Title,
                        Content = faqDoc.Content,
                        Source = faqDoc.Source ?? "FAQ",
                        Metadata = faqDoc.Metadata ?? new Dictionary<string, object>(),
                        IndexedAt = DateTime.UtcNow
                    };

                    documents.Add(document);
                }

                return documents;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse FAQ JSON: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load FAQ documents: {ex.Message}", ex);
            }
        }

        private class FaqCollection
        {
            public List<FaqDocument> Documents { get; set; } = new();
        }

        private class FaqDocument
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public Dictionary<string, object> Metadata { get; set; } = new();
        }
    }
}