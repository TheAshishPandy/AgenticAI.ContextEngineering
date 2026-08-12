// Core/Search/DocumentUploader.cs
using Microsoft.Extensions.Logging;
using SmartChatBot.ContextEngineering.Core.Interfaces;
using SmartChatBot.ContextEngineering.Core.Models;
using SmartChatBot.ContextEngineering.Core.Search;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SmartChatBot.ContextEngineering.Core.Upload
{
    /// <summary>
    /// Handles document uploading, processing, and indexing
    /// </summary>
    public class DocumentUploader
    {
        private readonly SearchIndex _searchIndex;
        private readonly IEmbeddingGenerator _embeddingGenerator;
        private readonly ILogger<DocumentUploader> _logger;
        private readonly SearchOptions _options;
        private readonly Dictionary<string, string> _fileExtensions = new()
        {
            { ".txt", "Text" },
            { ".md", "Markdown" },
            { ".pdf", "PDF" },
            { ".docx", "Word" },
            { ".xlsx", "Excel" },
            { ".pptx", "PowerPoint" },
            { ".cs", "Code" },
            { ".py", "Code" },
            { ".js", "Code" },
            { ".ts", "Code" },
            { ".go", "Code" },
            { ".rs", "Code" },
            { ".java", "Code" },
            { ".cpp", "Code" },
            { ".json", "Text" },
            { ".xml", "Text" },
            { ".html", "Text" },
            { ".css", "Code" },
            { ".sql", "Code" }
        };

        public DocumentUploader(
            SearchIndex searchIndex,
            IEmbeddingGenerator embeddingGenerator,
            ILogger<DocumentUploader> logger,
            SearchOptions options)
        {
            _searchIndex = searchIndex;
            _embeddingGenerator = embeddingGenerator;
            _logger = logger;
            _options = options;
        }

        /// <summary>
        /// Upload and index a single document
        /// </summary>
        public async Task<DocumentUploadResponse> UploadDocumentAsync(
            DocumentUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = new DocumentUploadResponse();

            try
            {
                _logger.LogInformation($"Processing document: {request.Title}");

                // Get content from file or use provided content
                string content = request.Content ?? string.Empty;

                if (!string.IsNullOrEmpty(request.FilePath) && string.IsNullOrEmpty(request.Content))
                {
                    content = await File.ReadAllTextAsync(request.FilePath, cancellationToken);
                    var ext = Path.GetExtension(request.FilePath).ToLower();
                    request.Type = _fileExtensions.TryGetValue(ext, out var type)
                        ? Enum.TryParse<DocumentType>(type, out var docType) ? docType : DocumentType.Text
                        : DocumentType.Text;
                }

                if (string.IsNullOrEmpty(content))
                {
                    response.Success = false;
                    response.Error = "No content provided";
                    return response;
                }

                // Chunk the document
                var chunks = ChunkDocument(content);
                response.ChunkCount = chunks.Count;

                // Create document ID
                var docId = Guid.NewGuid().ToString();

                // Create and index each chunk
                var indexedChunks = new List<Document>();
                foreach (var (chunkContent, index) in chunks.Select((c, i) => (c, i)))
                {
                    var chunkDoc = new Document
                    {
                        Id = $"{docId}-{index}",
                        Title = $"{request.Title} (Part {index + 1})",
                        Content = chunkContent,
                        Source = request.Source ?? request.FilePath ?? "Uploaded Document",
                        Metadata = new Dictionary<string, object>(request.Metadata)
                        {
                            ["parent_id"] = docId,
                            ["chunk_index"] = index,
                            ["total_chunks"] = chunks.Count,
                            ["document_type"] = request.Type.ToString()
                        }
                    };

                    // Generate embedding for the chunk
                    if (_embeddingGenerator.IsEnabled)
                    {
                        try
                        {
                            chunkDoc.Embedding = await _embeddingGenerator.GenerateEmbeddingAsync(
                                chunkContent,
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to generate embedding for chunk {index}: {ex.Message}");
                        }
                    }

                    // Add to search index
                    _searchIndex.IndexDocument(chunkDoc);
                    indexedChunks.Add(chunkDoc);
                }

                // Store parent document metadata
                var parentDoc = new Document
                {
                    Id = docId,
                    Title = request.Title,
                    Content = content,
                    Source = request.Source ?? request.FilePath ?? "Uploaded Document",
                    Metadata = new Dictionary<string, object>(request.Metadata)
                    {
                        ["total_chunks"] = chunks.Count,
                        ["document_type"] = request.Type.ToString(),
                        ["is_parent"] = true
                    }
                };
                _searchIndex.IndexDocument(parentDoc);

                response.DocumentId = docId;
                response.Success = true;
                response.IndexedAt = DateTime.UtcNow;
                response.TokenCount = content.Length / 4; // Rough estimation

                _logger.LogInformation($"✅ Document uploaded: {request.Title} ({chunks.Count} chunks)");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to upload document: {request.Title}");
                response.Success = false;
                response.Error = ex.Message;
                return response;
            }
        }

        /// <summary>
        /// Upload multiple documents
        /// </summary>
        public async Task<BatchUploadResponse> UploadDocumentsAsync(
            List<DocumentUploadRequest> requests,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = new BatchUploadResponse();

            foreach (var request in requests)
            {
                var result = await UploadDocumentAsync(request, cancellationToken);
                response.Results.Add(result);
                response.TotalDocuments++;

                if (result.Success)
                    response.SuccessfulUploads++;
                else
                    response.FailedUploads++;
            }

            stopwatch.Stop();
            response.ProcessingTime = stopwatch.Elapsed;

            _logger.LogInformation(
                $"Batch upload complete: {response.SuccessfulUploads}/{response.TotalDocuments} successful");

            return response;
        }

        /// <summary>
        /// Upload documents from a directory
        /// </summary>
        public async Task<BatchUploadResponse> UploadDirectoryAsync(
            string directoryPath,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            var requests = new List<DocumentUploadRequest>();

            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

            var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
            var supportedExtensions = _fileExtensions.Keys.ToHashSet();

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLower();
                if (!supportedExtensions.Contains(ext)) continue;

                requests.Add(new DocumentUploadRequest
                {
                    FilePath = file,
                    Title = Path.GetFileNameWithoutExtension(file),
                    Source = source ?? directoryPath,
                    Type = _fileExtensions.TryGetValue(ext, out var type)
                        ? Enum.TryParse<DocumentType>(type, out var docType) ? docType : DocumentType.Text
                        : DocumentType.Text
                });
            }

            return await UploadDocumentsAsync(requests, cancellationToken);
        }

        /// <summary>
        /// Chunk a document into smaller pieces
        /// </summary>
        private List<string> ChunkDocument(string content, int chunkSize = 500)
        {
            var chunks = new List<string>();
            var words = content.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length <= chunkSize)
            {
                chunks.Add(content);
                return chunks;
            }

            var currentChunk = new List<string>();
            var currentSize = 0;

            foreach (var word in words)
            {
                currentChunk.Add(word);
                currentSize++;

                if (currentSize >= chunkSize)
                {
                    chunks.Add(string.Join(" ", currentChunk));
                    currentChunk.Clear();
                    currentSize = 0;
                }
            }

            if (currentChunk.Any())
            {
                chunks.Add(string.Join(" ", currentChunk));
            }

            return chunks;
        }
    }
}