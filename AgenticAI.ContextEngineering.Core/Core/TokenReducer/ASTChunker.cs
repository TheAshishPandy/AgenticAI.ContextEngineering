// Core/TokenReducer/ASTChunker.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.TokenReducer
{
    /// <summary>
    /// AST-based code chunking with regex fallback
    /// </summary>
    public class ASTChunker
    {
        private readonly ILogger<ASTChunker> _logger;
        private readonly TokenReducerConfig _config;
        private readonly Dictionary<string, string> _languageExtensions;

        public ASTChunker(ILogger<ASTChunker> logger, TokenReducerConfig config)
        {
            _logger = logger;
            _config = config;
            _languageExtensions = new Dictionary<string, string>
            {
                { ".cs", "csharp" },
                { ".py", "python" },
                { ".js", "javascript" },
                { ".ts", "typescript" },
                { ".go", "go" },
                { ".rs", "rust" },
                { ".java", "java" },
                { ".cpp", "cpp" },
                { ".c", "c" },
                { ".h", "h" },
                { ".rb", "ruby" },
                { ".php", "php" },
                { ".swift", "swift" },
                { ".kt", "kotlin" },
                { ".scala", "scala" },
                { ".m", "objective-c" }
            };
        }

        /// <summary>
        /// Chunk files using AST parsing with regex fallback
        /// </summary>
        public async Task<List<CodeChunk>> ChunkFilesAsync(
            List<string> filePaths,
            CancellationToken cancellationToken = default)
        {
            var chunks = new List<CodeChunk>();

            foreach (var filePath in filePaths)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                    var extension = Path.GetExtension(filePath).ToLower();
                    var language = GetLanguage(extension);

                    var fileChunks = _config.ASTChunkingEnabled
                        ? await ChunkWithASTAsync(content, filePath, language, cancellationToken)
                        : ChunkWithRegex(content, filePath, language);

                    chunks.AddRange(fileChunks);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to chunk file: {filePath}");
                }
            }

            _logger.LogDebug($"Chunked {chunks.Count} chunks from {filePaths.Count} files");
            return chunks;
        }

        /// <summary>
        /// AST-based chunking (simplified - uses regex for structure detection)
        /// </summary>
        private async Task<List<CodeChunk>> ChunkWithASTAsync(
            string content,
            string filePath,
            string language,
            CancellationToken cancellationToken)
        {
            // In production, use TreeSitter.NET for actual AST parsing
            // For now, use enhanced regex-based chunking
            return ChunkWithRegex(content, filePath, language);
        }

        /// <summary>
        /// Regex-based chunking with structure detection
        /// </summary>
        private List<CodeChunk> ChunkWithRegex(string content, string filePath, string language)
        {
            var chunks = new List<CodeChunk>();

            // Find functions/classes using regex
            var patterns = GetStructurePatterns(language);
            var matches = new List<Match>();

            foreach (var pattern in patterns)
            {
                matches.AddRange(Regex.Matches(content, pattern, RegexOptions.Multiline).Cast<Match>());
            }

            matches = matches.OrderBy(m => m.Index).ToList();

            if (matches.Count == 0)
            {
                // Fallback: chunk by paragraphs
                return ChunkByParagraphs(content, filePath, language);
            }

            // Process each matched structure
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var startIndex = match.Index;
                var endIndex = i < matches.Count - 1 ? matches[i + 1].Index : content.Length;

                var chunkContent = content[startIndex..endIndex].Trim();
                var startLine = content[..startIndex].Count(c => c == '\n') + 1;
                var endLine = content[..endIndex].Count(c => c == '\n');

                var symbols = ExtractSymbols(chunkContent, language);
                var imports = ExtractImports(chunkContent, language);

                chunks.Add(new CodeChunk
                {
                    FilePath = filePath,
                    Language = language,
                    Content = chunkContent,
                    StartLine = startLine,
                    EndLine = endLine,
                    Symbols = symbols,
                    Imports = imports,
                    Dependencies = ExtractDependencies(chunkContent, language)
                });
            }

            return chunks;
        }

        /// <summary>
        /// Get structure patterns for different languages
        /// </summary>
        private List<string> GetStructurePatterns(string language)
        {
            return language switch
            {
                "csharp" => new List<string>
                {
                    @"(?:public|private|protected|internal|static|async)?\s*(?:class|struct|interface|enum|record)\s+(\w+)",
                    @"(?:public|private|protected|internal|static|async)?\s*(?:void|Task|Task<T>|[\w<>]+)\s+(\w+)\s*\("
                },
                "python" => new List<string>
                {
                    @"(?:class)\s+(\w+)",
                    @"(?:def)\s+(\w+)\s*\("
                },
                "javascript" => new List<string>
                {
                    @"(?:class)\s+(\w+)",
                    @"(?:function)\s+(\w+)\s*\(",
                    @"(?:const|let|var)\s+(\w+)\s*=\s*(?:function|\(.*\)\s*=>)"
                },
                "typescript" => new List<string>
                {
                    @"(?:class|interface|type|enum)\s+(\w+)",
                    @"(?:function)\s+(\w+)\s*\(",
                    @"(?:const|let|var)\s+(\w+)\s*=\s*(?:function|\(.*\)\s*=>)"
                },
                "go" => new List<string>
                {
                    @"(?:type)\s+(\w+)\s+(?:struct|interface)",
                    @"(?:func)\s+(\w+)\s*\("
                },
                "java" => new List<string>
                {
                    @"(?:public|private|protected)?\s*(?:class|interface|enum)\s+(\w+)",
                    @"(?:public|private|protected)?\s*(?:void|[\w<>]+)\s+(\w+)\s*\("
                },
                _ => new List<string>
                {
                    @"(?:class|struct|interface|enum|function|def)\s+(\w+)"
                }
            };
        }

        /// <summary>
        /// Chunk by paragraphs (fallback)
        /// </summary>
        private List<CodeChunk> ChunkByParagraphs(string content, string filePath, string language)
        {
            var chunks = new List<CodeChunk>();
            var paragraphs = Regex.Split(content, @"(?:\r?\n){2,}");
            int lineNum = 1;

            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para)) continue;

                chunks.Add(new CodeChunk
                {
                    FilePath = filePath,
                    Language = language,
                    Content = para.Trim(),
                    StartLine = lineNum,
                    EndLine = lineNum + para.Count(c => c == '\n'),
                    Symbols = ExtractSymbols(para, language),
                    Imports = ExtractImports(para, language)
                });

                lineNum += para.Count(c => c == '\n') + 1;
            }

            return chunks;
        }

        /// <summary>
        /// Extract symbols from content
        /// </summary>
        private List<string> ExtractSymbols(string content, string language)
        {
            var symbols = new List<string>();
            var patterns = GetSymbolPatterns(language);

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(content, pattern);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var symbol = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(symbol) && !symbols.Contains(symbol))
                        {
                            symbols.Add(symbol);
                        }
                    }
                }
            }

            return symbols;
        }

        /// <summary>
        /// Get symbol patterns for different languages
        /// </summary>
        private List<string> GetSymbolPatterns(string language)
        {
            return language switch
            {
                "csharp" => new List<string>
                {
                    @"\b(class|struct|interface|enum|record)\s+(\w+)",
                    @"\b(public|private|protected|internal)\s+(void|string|int|bool|Task)\s+(\w+)",
                    @"\b(private|public|protected|internal)\s+(\w+)\s*\{"
                },
                "python" => new List<string>
                {
                    @"\bclass\s+(\w+)",
                    @"\bdef\s+(\w+)",
                    @"\b(?:self\.)?(\w+)\s*="
                },
                "javascript" => new List<string>
                {
                    @"\bclass\s+(\w+)",
                    @"\bfunction\s+(\w+)",
                    @"\b(?:const|let|var)\s+(\w+)",
                    @"\b(?:export\s+)?(?:default\s+)?(\w+)"
                },
                _ => new List<string>
                {
                    @"\b(class|struct|interface|enum|function|def)\s+(\w+)",
                    @"\b(const|let|var)\s+(\w+)"
                }
            };
        }

        /// <summary>
        /// Extract imports from content
        /// </summary>
        private List<string> ExtractImports(string content, string language)
        {
            var imports = new List<string>();
            var patterns = GetImportPatterns(language);

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(content, pattern, RegexOptions.Multiline);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var import = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(import) && !imports.Contains(import))
                        {
                            imports.Add(import);
                        }
                    }
                }
            }

            return imports;
        }

        /// <summary>
        /// Get import patterns for different languages
        /// </summary>
        private List<string> GetImportPatterns(string language)
        {
            return language switch
            {
                "csharp" => new List<string> { @"^\s*using\s+([\w.]+)" },
                "python" => new List<string> { @"^\s*(?:import|from)\s+([\w.]+)" },
                "javascript" => new List<string> { @"^\s*(?:import|require)\s*\(?['""]([^'""]+)['""]" },
                "typescript" => new List<string> { @"^\s*(?:import|from)\s+['""]([^'""]+)['""]" },
                "go" => new List<string> { @"^\s*import\s+['""]([^'""]+)['""]" },
                "java" => new List<string> { @"^\s*import\s+([\w.]+)" },
                _ => new List<string>()
            };
        }

        /// <summary>
        /// Extract dependencies from content
        /// </summary>
        private List<string> ExtractDependencies(string content, string language)
        {
            var dependencies = new List<string>();
            var patterns = new[]
            {
                @"(?:new|using)\s+([A-Z][a-zA-Z0-9_]+)",
                @"await\s+([A-Z][a-zA-Z0-9_]+)",
                @"return\s+([A-Z][a-zA-Z0-9_]+)",
                @"\(([A-Z][a-zA-Z0-9_]+)\)",
                @"<([A-Z][a-zA-Z0-9_]+)>"
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(content, pattern);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var dep = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(dep) && !dependencies.Contains(dep))
                        {
                            dependencies.Add(dep);
                        }
                    }
                }
            }

            return dependencies;
        }

        /// <summary>
        /// Get language from file extension
        /// </summary>
        private string GetLanguage(string extension)
        {
            return _languageExtensions.GetValueOrDefault(extension, "unknown");
        }
    }
}