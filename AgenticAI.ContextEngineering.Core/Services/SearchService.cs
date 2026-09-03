// AgenticAI.ContextEngineering.Core/Services/SearchService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class SearchService : ISearchService
    {
        private readonly HybridSearchEngine _hybridSearchEngine;
        private readonly IEmbeddingGenerator _embeddingGenerator;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SearchService> _logger;
        private readonly IKVCache _kvCache;
        private readonly ITokenCache _tokenCache;
        private readonly IAIResponseService _aiResponseService; 
        private readonly int _defaultTopResults;
        private readonly double _minRelevanceScore;
        private readonly bool _enableCaching;
        private readonly bool _enableAIQueryGeneration;
        private readonly int _maxQueryVariants;

        private const string SEARCH_CACHE_PREFIX = "search:result:";
        private const string LEXICAL_CACHE_PREFIX = "search:lexical:";
        private const string SEMANTIC_CACHE_PREFIX = "search:semantic:";
        private const string CONTEXT_CACHE_PREFIX = "search:context:";
        private const string TOKEN_COUNT_PREFIX = "token:count:";
        private const string EMBEDDING_CACHE_PREFIX = "embedding:";

        public SearchService(
            HybridSearchEngine hybridSearchEngine,
            IEmbeddingGenerator embeddingGenerator,
            IConfiguration configuration,
            ILogger<SearchService> logger,
            IKVCache kvCache,
            ITokenCache tokenCache,
            IAIResponseService aiResponseService = null) // ✅ Optional
        {
            _hybridSearchEngine = hybridSearchEngine ?? throw new ArgumentNullException(nameof(hybridSearchEngine));
            _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kvCache = kvCache ?? throw new ArgumentNullException(nameof(kvCache));
            _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
            _aiResponseService = aiResponseService;

            _defaultTopResults = _configuration.GetValue<int>("Search:DefaultTopK", 10);
            _minRelevanceScore = _configuration.GetValue<double>("Search:MinimumRelevanceScore", 0.3);
            _enableCaching = _configuration.GetValue<bool>("Search:EnableCaching", true);
            _enableAIQueryGeneration = _configuration.GetValue<bool>("Search:EnableAIQueryGeneration", true);
            _maxQueryVariants = _configuration.GetValue<int>("Search:MaxQueryVariants", 5);
        }

        public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                ValidateAndNormalizeRequest(request);
                _logger.LogDebug("Performing {Algorithm} search for query: '{Query}'", request.Algorithm, request.Query);

                // ✅ Check cache
                if (_enableCaching)
                {
                    var cacheKey = GenerateCacheKey(request);
                    var cached = await _kvCache.GetAsync<SearchResponse>(cacheKey);
                    if (cached != null)
                    {
                        _logger.LogDebug("✅ Cache HIT for: '{Query}'", request.Query);
                        cached.ProcessingTime = stopwatch.Elapsed;
                        cached.Metadata ??= new Dictionary<string, object>();
                        cached.Metadata["FromCache"] = true;
                        return cached;
                    }
                }

                // ✅ Optimize query tokens
                var optimizedQuery = await OptimizeQueryTokensAsync(request.Query, cancellationToken);

                var libraryRequest = new SearchRequest
                {
                    Query = optimizedQuery,
                    Algorithm = request.Algorithm,
                    TopResults = request.TopResults,
                    MinimumRelevanceScore = request.MinimumRelevanceScore
                };

                // ✅ Handle embedding for semantic/hybrid search
                if (request.Algorithm == SearchAlgorithm.Cosine || request.Algorithm == SearchAlgorithm.Hybrid)
                {
                    try
                    {
                        if (request.QueryVector == null || request.QueryVector.Length == 0)
                        {
                            var embedding = await GetCachedEmbeddingAsync(request.Query);
                            request.QueryVector = embedding;
                            libraryRequest.QueryVector = embedding;
                        }
                        else
                        {
                            libraryRequest.QueryVector = request.QueryVector;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate embedding, falling back to lexical search");
                        request.Algorithm = SearchAlgorithm.BM25;
                        libraryRequest.Algorithm = SearchAlgorithm.BM25;
                        libraryRequest.QueryVector = null;
                    }
                }

                // ✅ Perform hybrid search
                var libraryResults = await _hybridSearchEngine.HybridSearchAsync(libraryRequest, cancellationToken);

                if (libraryResults?.Results == null)
                {
                    return CreateEmptyResponse(request, stopwatch);
                }

                var response = new SearchResponse
                {
                    Results = libraryResults.Results,
                    TotalCount = libraryResults.Results.Count,
                    SearchMethod = libraryResults.SearchMethod,
                    ProcessingTime = libraryResults.ProcessingTime,
                    Metadata = libraryResults.Metadata ?? new Dictionary<string, object>()
                };

                response.Metadata["Query"] = request.Query;
                response.Metadata["TopResults"] = request.TopResults;
                response.Metadata["FromCache"] = false;

                // ✅ Cache results
                if (_enableCaching && response.Results.Any())
                {
                    var cacheKey = GenerateCacheKey(request);
                    await _kvCache.SetAsync(cacheKey, response, TimeSpan.FromHours(1));
                }

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error in search for: '{Query}'", request.Query);
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    SearchMethod = "Error",
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object> { ["error"] = ex.Message }
                };
            }
        }

        public async Task<SearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    SearchMethod = "Error",
                    Metadata = new Dictionary<string, object> { ["error"] = "Query cannot be empty" }
                };
            }

            var request = new SearchRequest
            {
                Query = query,
                TopResults = _defaultTopResults,
                MinimumRelevanceScore = _minRelevanceScore,
                Algorithm = SearchAlgorithm.Hybrid
            };

            return await SearchAsync(request, cancellationToken);
        }

        public async Task<SearchResponse> LexicalSearchAsync(string query, int topResults = 10, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{LEXICAL_CACHE_PREFIX}{query.GetHashCode()}_{topResults}";
            var cached = await _kvCache.GetAsync<SearchResponse>(cacheKey);
            if (cached != null) return cached;

            var request = new SearchRequest
            {
                Query = query,
                TopResults = topResults,
                MinimumRelevanceScore = _minRelevanceScore,
                Algorithm = SearchAlgorithm.BM25
            };

            var response = await SearchAsync(request, cancellationToken);
            if (response.Results.Any())
            {
                await _kvCache.SetAsync(cacheKey, response, TimeSpan.FromHours(1));
            }

            return response;
        }

        public async Task<SearchResponse> SemanticSearchAsync(string query, int topResults = 10, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{SEMANTIC_CACHE_PREFIX}{query.GetHashCode()}_{topResults}";
            var cached = await _kvCache.GetAsync<SearchResponse>(cacheKey);
            if (cached != null) return cached;

            var request = new SearchRequest
            {
                Query = query,
                TopResults = topResults,
                MinimumRelevanceScore = _minRelevanceScore,
                Algorithm = SearchAlgorithm.Cosine
            };

            var response = await SearchAsync(request, cancellationToken);
            if (response.Results.Any())
            {
                await _kvCache.SetAsync(cacheKey, response, TimeSpan.FromHours(1));
            }

            return response;
        }

        public async Task<SearchResponse> GetRelevantContextAsync(
            string query,
            SearchContext? context = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    SearchMethod = "Error",
                    Metadata = new Dictionary<string, object> { ["error"] = "Query cannot be empty" }
                };
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var cacheKey = $"{CONTEXT_CACHE_PREFIX}{query.GetHashCode()}";
                var cached = await _kvCache.GetAsync<List<SearchResult>>(cacheKey);
                if (cached?.Any() == true)
                {
                    return new SearchResponse
                    {
                        Results = cached,
                        TotalCount = cached.Count,
                        SearchMethod = "Context Cache",
                        ProcessingTime = stopwatch.Elapsed,
                        Metadata = new Dictionary<string, object> { ["FromCache"] = true }
                    };
                }

                // ✅ Generate query variants (now with AI support)
                var variants = await GenerateQueryVariantsAsync(query, context, cancellationToken);
                var allResults = new List<List<SearchResult>>();

                foreach (var variant in variants)
                {
                    var variantRequest = new SearchRequest
                    {
                        Query = variant,
                        TopResults = _defaultTopResults,
                        MinimumRelevanceScore = _minRelevanceScore,
                        Algorithm = SearchAlgorithm.Hybrid
                    };

                    if (context != null)
                    {
                        variantRequest.Filters = new Dictionary<string, object>
                        {
                            ["ConversationLength"] = context.ConversationLength,
                            ["RecentUserMessages"] = string.Join(" | ", context.RecentUserMessages),
                            ["RecentBotMessages"] = string.Join(" | ", context.RecentBotMessages)
                        };
                    }

                    var response = await SearchAsync(variantRequest, cancellationToken);
                    if (response?.Results?.Any() == true)
                    {
                        allResults.Add(response.Results);
                    }
                }

                var fused = FuseResults(allResults);
                if (fused.Any())
                {
                    await _kvCache.SetAsync(cacheKey, fused, TimeSpan.FromHours(6));
                }

                return new SearchResponse
                {
                    Results = fused,
                    TotalCount = fused.Count,
                    SearchMethod = "Context Search",
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Variants"] = variants.Count,
                        ["SuccessfulVariants"] = allResults.Count
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error getting context for: '{Query}'", query);
                return new SearchResponse
                {
                    Results = new List<SearchResult>(),
                    SearchMethod = "Error",
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object> { ["error"] = ex.Message }
                };
            }
        }

        /// <summary>
        /// ✅ ENHANCED: Generate query variants using AI + Rules
        /// </summary>
        public async Task<List<string>> GenerateQueryVariantsAsync(
            string query,
            SearchContext? context = null,
            CancellationToken cancellationToken = default)
        {
            var variants = new List<string> { query };

            if (string.IsNullOrWhiteSpace(query))
                return variants;

            try
            {
                // ✅ 1. AI-Powered Query Generation (NEW)
                if (_enableAIQueryGeneration && _aiResponseService != null)
                {
                    try
                    {
                        var aiVariants = await GenerateAIQueryVariantsAsync(query, context, cancellationToken);
                        foreach (var v in aiVariants)
                        {
                            if (!variants.Contains(v) && !string.IsNullOrEmpty(v))
                                variants.Add(v);
                        }
                        _logger.LogInformation($"🤖 AI generated {aiVariants.Count} query variants for: '{query}'");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AI query generation failed, falling back to rule-based");
                    }
                }

                // ✅ 2. Context-based variants (existing)
                if (context?.RecentUserMessages?.Any() == true)
                {
                    var lastUserMsg = context.RecentUserMessages.LastOrDefault();
                    if (!string.IsNullOrEmpty(lastUserMsg) && lastUserMsg != query)
                    {
                        variants.Add($"{lastUserMsg} {query}");
                    }
                }

                // ✅ 3. Stop words removal (existing)
                var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were",
                    "and", "or", "but", "for", "nor", "on", "at", "to", "by", "with" };
                var words = query.Split(' ');
                var cleanQuery = string.Join(" ", words.Where(w => !stopWords.Contains(w.ToLower())));
                if (!variants.Contains(cleanQuery) && cleanQuery != query)
                    variants.Add(cleanQuery);

                // ✅ 4. Phrase extraction (existing)
                if (words.Length > 3)
                {
                    for (int i = 0; i < words.Length - 1; i++)
                    {
                        var phrase = $"{words[i]} {words[i + 1]}";
                        if (!variants.Contains(phrase) && phrase != query)
                            variants.Add(phrase);
                    }
                }

                // ✅ 5. Limit variants
                variants = variants.Take(_maxQueryVariants).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating variants for: '{Query}'", query);
            }

            return variants.Distinct().ToList();
        }

        /// <summary>
        /// ✅ Generate query variants using AI model
        /// </summary>
        private async Task<List<string>> GenerateAIQueryVariantsAsync(
            string query,
            SearchContext? context,
            CancellationToken cancellationToken)
        {
            var prompt = BuildAIVariantPrompt(query, context);

            var request = new AIResponseRequest
            {
                UserQuery = prompt,
                SystemPrompt = "You are a search query generator. Generate alternative search queries based on the user's question and context. Return ONLY a JSON array of strings, no explanation.",
                MaxTokens = 200,
                Temperature = 0.3f,
                UseCache = false
            };

            var response = await _aiResponseService.GenerateResponseAsync(request, cancellationToken);

            if (!response.IsSuccess || string.IsNullOrEmpty(response.Response))
            {
                return new List<string>();
            }

            return ParseVariantsFromResponse(response.Response);
        }

        /// <summary>
        /// ✅ Build prompt for AI variant generation
        /// </summary>
        private string BuildAIVariantPrompt(string query, SearchContext? context)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Generate alternative search queries for the following user question.");
            sb.AppendLine();

            if (context != null)
            {
                sb.AppendLine("### Conversation Context ###");
                if (context.RecentUserMessages?.Any() == true)
                {
                    sb.AppendLine("Recent user messages:");
                    foreach (var msg in context.RecentUserMessages.TakeLast(3))
                    {
                        sb.AppendLine($"  - {msg}");
                    }
                }
                if (context.RecentBotMessages?.Any() == true)
                {
                    sb.AppendLine("Recent bot responses:");
                    foreach (var msg in context.RecentBotMessages.TakeLast(3))
                    {
                        sb.AppendLine($"  - {msg}");
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine($"### User Question ###");
            sb.AppendLine(query);
            sb.AppendLine();
            sb.AppendLine($"Generate {_maxQueryVariants - 1} alternative search queries that:");
            sb.AppendLine("1. Use different wording and phrasing");
            sb.AppendLine("2. Capture different aspects of the question");
            sb.AppendLine("3. Consider the conversation context");
            sb.AppendLine("4. Would help find relevant information in a knowledge base");
            sb.AppendLine();
            sb.AppendLine("Return ONLY a JSON array of strings, like: [\"query1\", \"query2\", \"query3\"]");

            return sb.ToString();
        }

        /// <summary>
        /// ✅ Parse variants from AI response
        /// </summary>
        private List<string> ParseVariantsFromResponse(string response)
        {
            try
            {
                var cleaned = response.Trim();

                // Remove markdown code blocks
                if (cleaned.StartsWith("```json"))
                    cleaned = cleaned.Replace("```json", "").Replace("```", "").Trim();
                else if (cleaned.StartsWith("```"))
                    cleaned = cleaned.Replace("```", "").Trim();

                // Try JSON parsing
                var result = JsonSerializer.Deserialize<List<string>>(cleaned);
                if (result != null && result.Any())
                {
                    return result;
                }

                // Fallback: extract quoted strings
                var matches = System.Text.RegularExpressions.Regex.Matches(cleaned, "\"([^\"]*)\"");
                var extracted = new List<string>();
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var value = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(value))
                            extracted.Add(value);
                    }
                }

                return extracted.Any() ? extracted : new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse AI variants from response");
                return new List<string>();
            }
        }

        public List<SearchResult> FuseResults(List<List<SearchResult>> resultsLists)
        {
            if (resultsLists == null || !resultsLists.Any())
                return new List<SearchResult>();

            var valid = resultsLists.Where(list => list?.Any() == true).ToList();
            if (!valid.Any())
                return new List<SearchResult>();

            if (valid.Count == 1)
                return valid[0];

            try
            {
                const int k = 60;
                var scoreMap = new Dictionary<string, (SearchResult Result, double Score)>();

                foreach (var results in valid)
                {
                    for (int rank = 0; rank < results.Count; rank++)
                    {
                        var result = results[rank];
                        var fingerprint = ComputeFingerprint(result.Content);
                        var rrfScore = 1.0 / (k + rank + 1);

                        if (scoreMap.ContainsKey(fingerprint))
                        {
                            var existing = scoreMap[fingerprint];
                            scoreMap[fingerprint] = (existing.Result, existing.Score + rrfScore);
                        }
                        else
                        {
                            scoreMap[fingerprint] = (result, rrfScore);
                        }
                    }
                }

                return scoreMap.OrderByDescending(x => x.Value.Score)
                               .Select(x => x.Value.Result)
                               .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fusing results");
                return valid.FirstOrDefault() ?? new List<SearchResult>();
            }
        }

        public async Task ClearSearchCacheAsync()
        {
            await _kvCache.ClearAsync();
            _logger.LogInformation("🗑️ Search cache cleared");
        }

        public async Task<Dictionary<string, CacheStatistics>> GetCacheStatsAsync()
        {
            return new Dictionary<string, CacheStatistics>
            {
                ["KV"] = _kvCache.GetStatistics()
            };
        }

        // ============================================================
        // PRIVATE HELPERS
        // ============================================================

        private async Task<string> OptimizeQueryTokensAsync(string query, CancellationToken cancellationToken)
        {
            var key = $"{TOKEN_COUNT_PREFIX}{query.GetHashCode()}";
            var count = await _tokenCache.GetCachedTokenCountAsync(key);

            if (count == 0)
            {
                count = _tokenCache.CountTokens(query);
                await _tokenCache.CacheTokenCountAsync(key, count, TimeSpan.FromHours(24));
            }

            return query;
        }

        private async Task<float[]> GetCachedEmbeddingAsync(string query)
        {
            var key = $"{EMBEDDING_CACHE_PREFIX}{query.GetHashCode()}";
            var cached = await _kvCache.GetAsync<float[]>(key);

            if (cached?.Length > 0)
                return cached;

            var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(query);
            if (embedding?.Length > 0)
            {
                await _kvCache.SetAsync(key, embedding, TimeSpan.FromHours(24));
            }

            return embedding;
        }

        private string GenerateCacheKey(SearchRequest request)
        {
            var key = $"{SEARCH_CACHE_PREFIX}{request.Algorithm}_{request.Query.GetHashCode()}_{request.TopResults}_{request.MinimumRelevanceScore}";

            if (request.Filters?.Any() == true)
            {
                var filterHash = string.Join("_", request.Filters.OrderBy(k => k.Key));
                key += $"_{filterHash.GetHashCode()}";
            }

            return key;
        }

        private SearchResponse CreateEmptyResponse(SearchRequest request, Stopwatch stopwatch)
        {
            return new SearchResponse
            {
                Results = new List<SearchResult>(),
                SearchMethod = "No Results",
                ProcessingTime = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    ["query"] = request.Query,
                    ["error"] = "No results found"
                }
            };
        }

        private void ValidateAndNormalizeRequest(SearchRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.Query))
                throw new ArgumentException("Query cannot be empty", nameof(request));

            if (request.TopResults <= 0)
                request.TopResults = _defaultTopResults;

            if (request.TopResults > 100)
                request.TopResults = 100;

            if (request.MinimumRelevanceScore <= 0)
                request.MinimumRelevanceScore = _minRelevanceScore;

            if (request.Algorithm != SearchAlgorithm.BM25 &&
                request.Algorithm != SearchAlgorithm.TFIDF &&
                request.Algorithm != SearchAlgorithm.Cosine &&
                request.Algorithm != SearchAlgorithm.Hybrid)
            {
                request.Algorithm = SearchAlgorithm.Hybrid;
            }
        }

        private string ComputeFingerprint(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            return new string(content.ToLowerInvariant()
                                   .Where(c => !char.IsWhiteSpace(c))
                                   .Take(100)
                                   .ToArray());
        }
    }
}