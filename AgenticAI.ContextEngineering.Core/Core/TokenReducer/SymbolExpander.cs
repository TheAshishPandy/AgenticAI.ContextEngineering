// Core/TokenReducer/SymbolExpander.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.TokenReducer
{
    /// <summary>
    /// 2-hop symbol expansion for code context
    /// </summary>
    public class SymbolExpander
    {
        private readonly ILogger<SymbolExpander> _logger;
        private readonly TokenReducerConfig _config;

        public SymbolExpander(ILogger<SymbolExpander> logger, TokenReducerConfig config)
        {
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// Expand symbols with 2-hop resolution
        /// </summary>
        public async Task<List<string>> ExpandSymbolsAsync(
            List<string> symbols,
            List<CodeChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            var expanded = new List<string>(symbols);
            var symbolSet = new HashSet<string>(symbols);

            // First hop: Find definitions
            foreach (var symbol in symbols)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var definition = FindSymbolDefinition(symbol, chunks);
                if (!string.IsNullOrEmpty(definition) && !symbolSet.Contains(definition))
                {
                    expanded.Add(definition);
                    symbolSet.Add(definition);
                }
            }

            // Second hop: Find usages and related symbols
            var newSymbols = expanded.ToList();
            foreach (var symbol in newSymbols)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var related = FindRelatedSymbols(symbol, chunks);
                foreach (var rel in related)
                {
                    if (!symbolSet.Contains(rel))
                    {
                        expanded.Add(rel);
                        symbolSet.Add(rel);
                    }
                }
            }

            _logger.LogDebug($"Expanded {symbols.Count} symbols to {expanded.Count}");
            return expanded;
        }

        /// <summary>
        /// Find symbol definition in chunks
        /// </summary>
        private string FindSymbolDefinition(string symbol, List<CodeChunk> chunks)
        {
            var patterns = new[]
            {
                $@"(?:class|struct|interface|enum|record)\s+{symbol}",
                $@"(?:def|function)\s+{symbol}",
                $@"(?:const|let|var)\s+{symbol}",
                $@"(?:public|private|protected|internal).*?\s+{symbol}"
            };

            foreach (var chunk in chunks)
            {
                foreach (var pattern in patterns)
                {
                    if (Regex.IsMatch(chunk.Content, pattern, RegexOptions.Multiline))
                    {
                        // Extract the containing structure
                        var lines = chunk.Content.Split('\n');
                        foreach (var line in lines)
                        {
                            if (Regex.IsMatch(line, pattern))
                            {
                                return symbol;
                            }
                        }
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Find related symbols in code
        /// </summary>
        private List<string> FindRelatedSymbols(string symbol, List<CodeChunk> chunks)
        {
            var related = new List<string>();
            var patterns = new[]
            {
                $@"{symbol}\.",
                $@"new\s+{symbol}",
                $@"{symbol}\s*=",
                $@"{symbol}\s*\(",
                $@"using\s+{symbol}"
            };

            foreach (var chunk in chunks)
            {
                foreach (var pattern in patterns)
                {
                    var matches = Regex.Matches(chunk.Content, pattern);
                    foreach (Match match in matches)
                    {
                        var matchText = match.Value;
                        // Extract potential related symbol
                        if (matchText.Contains('.'))
                        {
                            var parts = matchText.Split('.');
                            if (parts.Length > 1 && !related.Contains(parts[1]))
                            {
                                related.Add(parts[1]);
                            }
                        }
                    }
                }

                // Also check imports
                foreach (var import in chunk.Imports)
                {
                    if (import.Contains(symbol) && !related.Contains(import))
                    {
                        related.Add(import);
                    }
                }
            }

            return related.Distinct().ToList();
        }

        /// <summary>
        /// Get type information for a symbol
        /// </summary>
        public string GetSymbolType(string symbol, List<CodeChunk> chunks)
        {
            var patterns = new[]
            {
                $@"(?:class|struct|interface|enum)\s+{symbol}",
                $@"(?:def|function)\s+{symbol}",
                $@"(?:const|let|var)\s+{symbol}\s*:\s*(\w+)",
                $@"{symbol}\s*:\s*(\w+)"
            };

            foreach (var chunk in chunks)
            {
                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(chunk.Content, pattern);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }

            return "unknown";
        }

        /// <summary>
        /// Check if symbol is a class/type
        /// </summary>
        public bool IsClassSymbol(string symbol, List<CodeChunk> chunks)
        {
            var patterns = new[]
            {
                $@"class\s+{symbol}",
                $@"struct\s+{symbol}",
                $@"interface\s+{symbol}",
                $@"enum\s+{symbol}"
            };

            foreach (var chunk in chunks)
            {
                foreach (var pattern in patterns)
                {
                    if (Regex.IsMatch(chunk.Content, pattern))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Get all symbols in a chunk
        /// </summary>
        public List<string> ExtractSymbolsFromChunk(CodeChunk chunk)
        {
            var symbols = new List<string>();
            var patterns = new[]
            {
                @"\b([A-Z][a-zA-Z0-9_]+)\b",
                @"\b([a-z][a-zA-Z0-9_]+)\s*\(",
                @"\b(const|let|var)\s+([a-z][a-zA-Z0-9_]+)\b"
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(chunk.Content, pattern);
                foreach (Match match in matches)
                {
                    var symbol = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(symbol) && !symbols.Contains(symbol))
                    {
                        symbols.Add(symbol);
                    }
                }
            }

            return symbols;
        }
    }
}