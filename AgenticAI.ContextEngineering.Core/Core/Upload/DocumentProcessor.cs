// Core/Helper/DocumentUploaderHelper.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Core.Helper
{
    /// <summary>
    /// Helper class for document upload and processing operations
    /// </summary>
    public class DocumentUploaderHelper
    {
        private readonly Dictionary<string, string> _fileExtensions;
        private readonly Dictionary<string, DocumentType> _fileTypeMap;

        public DocumentUploaderHelper()
        {
            _fileExtensions = new Dictionary<string, string>
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
                { ".c", "Code" },
                { ".h", "Code" },
                { ".json", "Text" },
                { ".xml", "Text" },
                { ".html", "Text" },
                { ".css", "Code" },
                { ".sql", "Code" },
                { ".rb", "Code" },
                { ".php", "Code" },
                { ".swift", "Code" },
                { ".kt", "Code" },
                { ".scala", "Code" },
                { ".r", "Code" },
                { ".m", "Code" },
                { ".yaml", "Text" },
                { ".yml", "Text" },
                { ".toml", "Text" },
                { ".ini", "Text" },
                { ".cfg", "Text" },
                { ".conf", "Text" },
                { ".log", "Text" },
                { ".csv", "Excel" },
                { ".tsv", "Excel" },
                { ".xls", "Excel" },
                { ".ppt", "PowerPoint" },
                { ".doc", "Word" },
                { ".rtf", "Word" },
                { ".odt", "Word" },
                { ".ods", "Excel" },
                { ".odp", "PowerPoint" }
            };

            _fileTypeMap = _fileExtensions.ToDictionary(
                kvp => kvp.Key,
                kvp => Enum.TryParse<DocumentType>(kvp.Value, out var docType) ? docType : DocumentType.Text
            );
        }

        // ============================================================
        // FILE EXTENSION METHODS
        // ============================================================

        /// <summary>
        /// Get supported file extensions
        /// </summary>
        public HashSet<string> GetSupportedExtensions()
        {
            return _fileExtensions.Keys.ToHashSet();
        }

        /// <summary>
        /// Get supported extensions with descriptions
        /// </summary>
        public Dictionary<string, string> GetSupportedExtensionsWithDescriptions()
        {
            return new Dictionary<string, string>(_fileExtensions);
        }

        /// <summary>
        /// Get document type from file extension
        /// </summary>
        public DocumentType GetDocumentType(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return DocumentType.Text;

            extension = extension.ToLower();
            return _fileTypeMap.TryGetValue(extension, out var docType)
                ? docType
                : DocumentType.Text;
        }

        /// <summary>
        /// Get document type from file path
        /// </summary>
        public DocumentType GetDocumentTypeFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return DocumentType.Text;

            var ext = Path.GetExtension(filePath).ToLower();
            return GetDocumentType(ext);
        }

        /// <summary>
        /// Check if file extension is supported
        /// </summary>
        public bool IsSupportedExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return false;

            return _fileExtensions.ContainsKey(extension.ToLower());
        }

        /// <summary>
        /// Check if file is supported based on path
        /// </summary>
        public bool IsSupportedFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var ext = Path.GetExtension(filePath).ToLower();
            return IsSupportedExtension(ext);
        }

        // ============================================================
        // FILE CATEGORY METHODS
        // ============================================================

        /// <summary>
        /// Get file category (Text, Code, Document, etc.)
        /// </summary>
        public string GetFileCategory(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return "Unknown";

            var docType = GetDocumentType(extension);
            return docType switch
            {
                DocumentType.Text => "Text",
                DocumentType.Markdown => "Documentation",
                DocumentType.PDF => "Document",
                DocumentType.Word => "Document",
                DocumentType.Excel => "Spreadsheet",
                DocumentType.PowerPoint => "Presentation",
                DocumentType.Code => "Code",
                DocumentType.Image => "Image",
                _ => "Other"
            };
        }

        /// <summary>
        /// Get file category from file path
        /// </summary>
        public string GetFileCategoryFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "Unknown";

            var ext = Path.GetExtension(filePath).ToLower();
            return GetFileCategory(ext);
        }

        // ============================================================
        // FILE INFORMATION METHODS
        // ============================================================

        /// <summary>
        /// Get file size in human-readable format
        /// </summary>
        public string GetFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Extract file information
        /// </summary>
        public FileInfo? GetFileInfo(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            return new FileInfo(filePath);
        }

        /// <summary>
        /// Get detailed file information
        /// </summary>
        public DocumentFileInfo? GetDetailedFileInfo(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            var file = new FileInfo(filePath);
            var ext = Path.GetExtension(filePath).ToLower();

            return new DocumentFileInfo
            {
                FileName = file.Name,
                FilePath = file.FullName,
                Extension = ext,
                Size = file.Length,
                SizeDisplay = GetFileSize(file.Length),
                CreatedAt = file.CreationTimeUtc,
                ModifiedAt = file.LastWriteTimeUtc,
                DocumentType = GetDocumentType(ext),
                Category = GetFileCategory(ext),
                IsSupported = IsSupportedExtension(ext)
            };
        }

        /// <summary>
        /// Get file information from multiple files
        /// </summary>
        public List<DocumentFileInfo> GetMultipleFileInfo(List<string> filePaths)
        {
            var results = new List<DocumentFileInfo>();
            foreach (var path in filePaths)
            {
                var info = GetDetailedFileInfo(path);
                if (info != null)
                    results.Add(info);
            }
            return results;
        }

        // ============================================================
        // FILE VALIDATION METHODS
        // ============================================================

        /// <summary>
        /// Validate file before upload
        /// </summary>
        public ValidationResult ValidateFile(string filePath, long maxFileSize = 50 * 1024 * 1024)
        {
            var result = new ValidationResult();

            // Check if file exists
            if (!File.Exists(filePath))
            {
                result.IsValid = false;
                result.Error = "File does not exist";
                return result;
            }

            // Check file size
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > maxFileSize)
            {
                result.IsValid = false;
                result.Error = $"File size exceeds maximum allowed ({GetFileSize(maxFileSize)})";
                return result;
            }

            // Check extension
            var ext = Path.GetExtension(filePath).ToLower();
            if (!IsSupportedExtension(ext))
            {
                result.IsValid = false;
                result.Error = $"File type '{ext}' is not supported";
                result.SupportedExtensions = GetSupportedExtensions().ToList();
                return result;
            }

            result.IsValid = true;
            result.FileInfo = GetDetailedFileInfo(filePath);
            return result;
        }

        /// <summary>
        /// Validate multiple files
        /// </summary>
        public List<ValidationResult> ValidateFiles(List<string> filePaths, long maxFileSize = 50 * 1024 * 1024)
        {
            var results = new List<ValidationResult>();
            foreach (var path in filePaths)
            {
                results.Add(ValidateFile(path, maxFileSize));
            }
            return results;
        }

        // ============================================================
        // FILE CONTENT METHODS
        // ============================================================

        /// <summary>
        /// Read text content from file
        /// </summary>
        public async Task<string> ReadTextContentAsync(string filePath, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            return await File.ReadAllTextAsync(filePath, encoding);
        }

        /// <summary>
        /// Read file content as byte array
        /// </summary>
        public async Task<byte[]> ReadBinaryContentAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            return await File.ReadAllBytesAsync(filePath);
        }

        /// <summary>
        /// Read text content from multiple files
        /// </summary>
        public async Task<Dictionary<string, string>> ReadMultipleTextContentAsync(
            List<string> filePaths,
            Encoding? encoding = null)
        {
            var results = new Dictionary<string, string>();
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    var content = await ReadTextContentAsync(path, encoding);
                    results[path] = content;
                }
            }
            return results;
        }

        // ============================================================
        // CHUNKING METHODS
        // ============================================================

        /// <summary>
        /// Chunk text content into smaller pieces
        /// </summary>
        public List<string> ChunkText(string content, int chunkSize = 500, int overlap = 50)
        {
            if (string.IsNullOrEmpty(content))
                return new List<string>();

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

                    // Keep overlap
                    if (overlap > 0 && currentChunk.Count > overlap)
                    {
                        currentChunk = currentChunk.Skip(currentChunk.Count - overlap).ToList();
                        currentSize = currentChunk.Count;
                    }
                    else
                    {
                        currentChunk.Clear();
                        currentSize = 0;
                    }
                }
            }

            if (currentChunk.Any())
            {
                chunks.Add(string.Join(" ", currentChunk));
            }

            return chunks;
        }

        /// <summary>
        /// Chunk text by sentences
        /// </summary>
        public List<string> ChunkBySentences(string content, int maxSentences = 10)
        {
            if (string.IsNullOrEmpty(content))
                return new List<string>();

            // Split by sentence delimiters
            var sentences = Regex.Split(content, @"(?<=[.!?])\s+");
            var chunks = new List<string>();
            var currentChunk = new List<string>();

            foreach (var sentence in sentences)
            {
                currentChunk.Add(sentence);
                if (currentChunk.Count >= maxSentences)
                {
                    chunks.Add(string.Join(" ", currentChunk));
                    currentChunk.Clear();
                }
            }

            if (currentChunk.Any())
            {
                chunks.Add(string.Join(" ", currentChunk));
            }

            return chunks;
        }

        /// <summary>
        /// Chunk text by paragraphs
        /// </summary>
        public List<string> ChunkByParagraphs(string content)
        {
            if (string.IsNullOrEmpty(content))
                return new List<string>();

            var paragraphs = Regex.Split(content, @"(?:\r?\n){2,}");
            return paragraphs.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        }

        // ============================================================
        // DIRECTORY METHODS
        // ============================================================

        /// <summary>
        /// Get file statistics for a directory
        /// </summary>
        public DirectoryStatistics GetDirectoryStatistics(string directoryPath)
        {
            var stats = new DirectoryStatistics();

            if (!Directory.Exists(directoryPath))
                return stats;

            var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
            stats.TotalFiles = files.Length;

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLower();
                if (IsSupportedExtension(ext))
                {
                    stats.SupportedFiles++;
                    stats.TotalSize += new FileInfo(file).Length;

                    var docType = GetDocumentType(ext);
                    if (!stats.FileTypeCounts.ContainsKey(docType))
                        stats.FileTypeCounts[docType] = 0;
                    stats.FileTypeCounts[docType]++;
                }
                else
                {
                    stats.UnsupportedFiles++;
                }
            }

            stats.TotalSizeDisplay = GetFileSize(stats.TotalSize);
            return stats;
        }

        /// <summary>
        /// Get all supported files from a directory
        /// </summary>
        public List<string> GetSupportedFilesFromDirectory(string directoryPath)
        {
            var files = new List<string>();

            if (!Directory.Exists(directoryPath))
                return files;

            var allFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                if (IsSupportedFile(file))
                {
                    files.Add(file);
                }
            }

            return files;
        }

        /// <summary>
        /// Get file tree structure
        /// </summary>
        public Dictionary<string, List<string>> GetFileTree(string directoryPath)
        {
            var tree = new Dictionary<string, List<string>>();

            if (!Directory.Exists(directoryPath))
                return tree;

            var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories);
            foreach (var dir in directories)
            {
                var files = Directory.GetFiles(dir)
                    .Where(f => IsSupportedFile(f))
                    .Select(f => Path.GetFileName(f))
                    .ToList();

                if (files.Any())
                {
                    tree[dir] = files;
                }
            }

            return tree;
        }
    }

    // ============================================================
    // SUPPORTING CLASSES
    // ============================================================

    public class DocumentFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long Size { get; set; }
        public string SizeDisplay { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public DocumentType DocumentType { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsSupported { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? Error { get; set; }
        public DocumentFileInfo? FileInfo { get; set; }
        public List<string> SupportedExtensions { get; set; } = new();
    }

    public class DirectoryStatistics
    {
        public int TotalFiles { get; set; }
        public int SupportedFiles { get; set; }
        public int UnsupportedFiles { get; set; }
        public long TotalSize { get; set; }
        public string TotalSizeDisplay { get; set; } = "0 B";
        public Dictionary<DocumentType, int> FileTypeCounts { get; set; } = new();
    }
}