// AgenticAI.ContextEngineering.Core/Services/CachedFaqService.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    /// <summary>
    /// Cached FAQ Service - Uses MultiTierKVCache
    /// </summary>
    public class CachedFaqService : IFaqService
    {
        private readonly SearchIndex _searchIndex;
        private readonly LexicalSearch _lexicalSearch;
        private readonly IKVCache _cache; 
        private readonly ILogger<CachedFaqService> _logger;
        private readonly IOptions<FaqOptions> _options;

        private const string FAQ_INDEX_CACHE = "faq:index";
        private const string FAQ_SEARCH_PREFIX = "faq:search:";
        private const string FAQ_ANSWER_PREFIX = "faq:answer:";
        private const string FAQ_POPULAR_PREFIX = "faq:popular:";
        private const string FAQ_ALL_PREFIX = "faq:all:";
        private const string FAQ_CATEGORY_PREFIX = "faq:category:";
        private const string FAQ_LANGUAGE_PREFIX = "faq:language:";

        private bool _isInitialized = false;
        private string _currentFilePath = string.Empty;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public bool IsLoaded => _isInitialized;
        public int DocumentCount => _searchIndex?.Count ?? 0;

        public CachedFaqService(
            SearchIndex searchIndex,
            LexicalSearch lexicalSearch,
            IKVCache cache,
            IOptions<FaqOptions> options,
            ILogger<CachedFaqService> logger)
        {
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _lexicalSearch = lexicalSearch ?? throw new ArgumentNullException(nameof(lexicalSearch));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SetFaqFilePath(string filePath)
        {
            _currentFilePath = filePath;
            _logger.LogInformation($"📂 FAQ file path set to: {filePath}");
        }

        public async Task<bool> TryLoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                SetFaqFilePath(filePath);
                await LoadAndIndexFaqsAsync(filePath, cancellationToken);
                return _isInitialized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load FAQ from: {filePath}");
                return false;
            }
        }

        public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized) return;

            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                await LoadAndIndexFaqsAsync(_currentFilePath, cancellationToken);
            }
            else
            {
                var filePath = _options.Value.FaqFilePath ?? "Data/faq.json";
                _currentFilePath = filePath;
                await LoadAndIndexFaqsAsync(filePath, cancellationToken);
            }
        }

        public async Task LoadAndIndexFaqsAsync(string faqFilePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(faqFilePath))
                throw new ArgumentException("FAQ file path cannot be null or empty", nameof(faqFilePath));

            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                // ✅ Try cache first (MultiTier - Memory, File, Distributed)
                var cached = await _cache.GetAsync<List<Document>>(FAQ_INDEX_CACHE);
                if (cached != null && cached.Count > 0)
                {
                    _logger.LogInformation($"✅ FAQ loaded from MultiTier cache: {cached.Count} documents");
                    _searchIndex.Clear();
                    _searchIndex.IndexDocuments(cached);
                    _isInitialized = true;
                    return;
                }

                // ✅ Load from file
                _logger.LogInformation($"📂 Loading FAQ from: {faqFilePath}");

                if (!System.IO.File.Exists(faqFilePath))
                {
                    _logger.LogWarning($"❌ FAQ file not found: {faqFilePath}");
                    return;
                }

                var jsonContent = await System.IO.File.ReadAllTextAsync(faqFilePath, cancellationToken);

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
                };

                var faqCollection = System.Text.Json.JsonSerializer.Deserialize<FaqCollection>(jsonContent, options);

                if (faqCollection?.Documents == null || faqCollection.Documents.Count == 0)
                {
                    _logger.LogWarning("⚠️ No documents found in FAQ file");
                    return;
                }

                _logger.LogInformation($"📊 Found {faqCollection.Documents.Count} documents in JSON");

                var documents = faqCollection.Documents.Select(d => new Document
                {
                    Id = d.Id ?? Guid.NewGuid().ToString(),
                    Title = d.Title ?? "FAQ",
                    Content = d.Content ?? string.Empty,
                    Source = d.Source ?? "FAQ",
                    Metadata = d.Metadata ?? new Dictionary<string, object>()
                }).ToList();

                _searchIndex.Clear();
                _searchIndex.IndexDocuments(documents);
                _isInitialized = true;

                // ✅ Cache the index in MultiTier (Memory + File)
                await _cache.SetAsync(FAQ_INDEX_CACHE, documents, TimeSpan.FromDays(7));
                _logger.LogInformation($"✅ FAQ indexed and cached (MultiTier): {documents.Count} documents");
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task<List<SearchResult>> SearchFaqsAsync(
            string query,
            int topResults = 5,
            double minScore = 0.1,
            CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                await EnsureLoadedAsync(cancellationToken);
                if (!_isInitialized)
                {
                    _logger.LogWarning("⚠️ FAQ not loaded");
                    return new List<SearchResult>();
                }
            }

            // ✅ Try MultiTier cache
            var cacheKey = $"{FAQ_SEARCH_PREFIX}{query.GetHashCode()}_{topResults}_{minScore}";
            var cached = await _cache.GetAsync<List<SearchResult>>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"✅ FAQ search cache HIT (MultiTier): {query}");
                return cached;
            }

            _logger.LogInformation($"❌ FAQ search cache MISS: {query}");

            // ✅ Execute search
            var request = new SearchRequest
            {
                Query = query,
                TopResults = topResults,
                MinimumRelevanceScore = minScore,
                IncludeScoreBreakdown = true
            };

            var response = await _lexicalSearch.SearchAsync(request, cancellationToken);

            // ✅ Cache results in MultiTier
            if (response.Results.Count > 0)
            {
                await _cache.SetAsync(cacheKey, response.Results, TimeSpan.FromHours(1));
                _logger.LogInformation($"✅ FAQ search cached (MultiTier): {query}");
            }

            return response.Results;
        }

        public async Task<string> GetBestAnswerAsync(string query, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                await EnsureLoadedAsync(cancellationToken);
                if (!_isInitialized) return null;
            }

            // ✅ Try MultiTier cache
            var cacheKey = $"{FAQ_ANSWER_PREFIX}{query.GetHashCode()}";
            var cached = await _cache.GetAsync<string>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"✅ FAQ answer cache HIT (MultiTier): {query}");
                return cached;
            }

            _logger.LogInformation($"❌ FAQ answer cache MISS: {query}");

            var results = await SearchFaqsAsync(query, topResults: 1, minScore: 0.1, cancellationToken);

            if (results.Any() && results.First().Score > 0.1)
            {
                var answer = $"{results.First().Title}\n\n{results.First().Content}";

                await _cache.SetAsync(cacheKey, answer, TimeSpan.FromHours(24));
                _logger.LogInformation($"✅ FAQ answer cached (MultiTier): {query}");

                return answer;
            }

            return null;
        }

        public async Task<string> GetDetailedSearchAsync(string query, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                await EnsureLoadedAsync(cancellationToken);
                if (!_isInitialized) return "FAQ not loaded.";
            }

            var results = await SearchFaqsAsync(query, topResults: 5, minScore: 0, cancellationToken);

            if (!results.Any())
                return "No results found.";

            var output = $"📊 Search Results for: \"{query}\"\n";
            output += new string('=', 50) + "\n\n";

            foreach (var result in results)
            {
                output += $"📌 {result.Title}\n";
                output += $"   Score: {result.Score:F3}\n";
                output += $"   ID: {result.Id}\n";
                if (result.ScoreBreakdown != null)
                {
                    output += $"   Lexical Score: {result.ScoreBreakdown.LexicalScore:F3}\n";
                }
                output += $"   Preview: {result.Content?.Substring(0, Math.Min(100, result.Content?.Length ?? 0))}...\n";
                output += "\n";
            }

            return output;
        }

        public async Task<FaqSearchResponse> SearchAsync(FaqSearchRequest request, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                await EnsureLoadedAsync(cancellationToken);
                if (!_isInitialized)
                {
                    return new FaqSearchResponse
                    {
                        Error = "FAQ not loaded",
                        ProcessingTimeMs = 0
                    };
                }
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // ✅ Try MultiTier cache
            var cacheKey = $"{FAQ_SEARCH_PREFIX}{request.Query.GetHashCode()}_{request.TopResults}_{request.Language}_{request.Category}";
            var cached = await _cache.GetAsync<FaqSearchResponse>(cacheKey);
            if (cached != null)
            {
                cached.FromCache = true;
                cached.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                _logger.LogInformation($"✅ FAQ search cache HIT (MultiTier): {request.Query}");
                return cached;
            }

            _logger.LogInformation($"❌ FAQ search cache MISS: {request.Query}");

            var results = await SearchFaqsAsync(request.Query, request.TopResults * 2, request.MinimumConfidence, cancellationToken);

            var faqResults = results.Select(r => new FaqResult
            {
                Id = r.Id,
                Question = r.Title ?? r.Content,
                Answer = r.Content,
                Category = r.Metadata?.GetValueOrDefault("Category")?.ToString() ?? "General",
                RelevanceScore = r.Score,
                ScoreBreakdown = r.ScoreBreakdown
            }).ToList();

            var response = new FaqSearchResponse
            {
                Results = faqResults,
                TotalResults = faqResults.Count,
                FromCache = false,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };

            // ✅ Cache results in MultiTier
            if (request.UseCache && response.Results.Any())
            {
                await _cache.SetAsync(cacheKey, response, TimeSpan.FromHours(1));
                _logger.LogInformation($"✅ FAQ search cached (MultiTier): {request.Query}");
            }

            return response;
        }

        public Task<List<FaqItem>> GetAllFaqsAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                return Task.FromResult(new List<FaqItem>());
            }

            var documents = _searchIndex.GetAllDocuments();
            var faqs = documents.Select(d => new FaqItem
            {
                Id = d.Id,
                Question = d.Title,
                Answer = d.Content,
                Category = d.Metadata?.GetValueOrDefault("Category")?.ToString() ?? "General",
                Language = d.Metadata?.GetValueOrDefault("Language") is int lang ? lang : 1,
                Keywords = d.Metadata?.GetValueOrDefault("Keywords") as List<string> ?? new List<string>(),
                Tags = d.Metadata?.GetValueOrDefault("Tags") as List<string> ?? new List<string>(),
                Metadata = d.Metadata ?? new Dictionary<string, object>()
            }).ToList();

            return Task.FromResult(faqs);
        }

        public Task<FaqItem?> GetFaqByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                return Task.FromResult<FaqItem?>(null);

            var doc = _searchIndex.GetDocument(id);
            if (doc == null) return Task.FromResult<FaqItem?>(null);

            return Task.FromResult<FaqItem?>(new FaqItem
            {
                Id = doc.Id,
                Question = doc.Title,
                Answer = doc.Content,
                Category = doc.Metadata?.GetValueOrDefault("Category")?.ToString() ?? "General",
                Language = doc.Metadata?.GetValueOrDefault("Language") is int lang ? lang : 1,
                Keywords = doc.Metadata?.GetValueOrDefault("Keywords") as List<string> ?? new List<string>(),
                Tags = doc.Metadata?.GetValueOrDefault("Tags") as List<string> ?? new List<string>(),
                Metadata = doc.Metadata ?? new Dictionary<string, object>()
            });
        }

        public Task<List<FaqItem>> GetFaqsByCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                return Task.FromResult(new List<FaqItem>());

            var documents = _searchIndex.GetAllDocuments()
                .Where(d => d.Metadata?.GetValueOrDefault("Category")?.ToString() == category)
                .ToList();

            var faqs = documents.Select(d => new FaqItem
            {
                Id = d.Id,
                Question = d.Title,
                Answer = d.Content,
                Category = category,
                Language = d.Metadata?.GetValueOrDefault("Language") is int lang ? lang : 1,
                Keywords = d.Metadata?.GetValueOrDefault("Keywords") as List<string> ?? new List<string>(),
                Tags = d.Metadata?.GetValueOrDefault("Tags") as List<string> ?? new List<string>(),
                Metadata = d.Metadata ?? new Dictionary<string, object>()
            }).ToList();

            return Task.FromResult(faqs);
        }

        public Task<List<FaqItem>> GetFaqsByLanguageAsync(int language, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                return Task.FromResult(new List<FaqItem>());

            var documents = _searchIndex.GetAllDocuments()
                .Where(d => (d.Metadata?.GetValueOrDefault("Language") is int lang ? lang : 1) == language)
                .ToList();

            var faqs = documents.Select(d => new FaqItem
            {
                Id = d.Id,
                Question = d.Title,
                Answer = d.Content,
                Category = d.Metadata?.GetValueOrDefault("Category")?.ToString() ?? "General",
                Language = language,
                Keywords = d.Metadata?.GetValueOrDefault("Keywords") as List<string> ?? new List<string>(),
                Tags = d.Metadata?.GetValueOrDefault("Tags") as List<string> ?? new List<string>(),
                Metadata = d.Metadata ?? new Dictionary<string, object>()
            }).ToList();

            return Task.FromResult(faqs);
        }

        public async Task<List<SearchResult>> GetPopularFaqsAsync(int count = 10, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                await EnsureLoadedAsync(cancellationToken);
                if (!_isInitialized) return new List<SearchResult>();
            }

            var cacheKey = $"{FAQ_POPULAR_PREFIX}{count}";
            var cached = await _cache.GetAsync<List<SearchResult>>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"✅ Popular FAQs cache HIT (MultiTier)");
                return cached;
            }

            var allDocs = _searchIndex.GetAllDocuments();
            var popular = allDocs
                .OrderByDescending(d => d.Metadata?.GetValueOrDefault("popularity", 0) ?? 0)
                .Take(count)
                .Select(d => new SearchResult
                {
                    Id = d.Id,
                    Title = d.Title,
                    Content = d.Content,
                    Source = d.Source,
                    Score = 1.0,
                    Metadata = d.Metadata
                })
                .ToList();

            await _cache.SetAsync(cacheKey, popular, TimeSpan.FromHours(24));
            _logger.LogInformation($"✅ Popular FAQs cached (MultiTier): {popular.Count} items");

            return popular;
        }

        public async Task ReloadFaqsAsync(CancellationToken cancellationToken = default)
        {
            _isInitialized = false;
            _searchIndex.Clear();
            await _cache.RemoveAsync(FAQ_INDEX_CACHE);
            _logger.LogInformation("🗑️ FAQ cache cleared, ready for reload");

            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                await LoadAndIndexFaqsAsync(_currentFilePath, cancellationToken);
            }
        }

        public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
        {
            await _cache.RemoveByPatternAsync("faq:*");
            _logger.LogInformation("🗑️ All FAQ caches cleared");
        }

        public async Task<FaqStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var stats = new FaqStatistics
            {
                TotalFaqs = _searchIndex.Count,
                LastUpdated = DateTime.UtcNow,
                IsIndexed = _isInitialized,
                SearchIndexCount = _searchIndex.Count
            };

            try
            {
                var allDocs = await _cache.GetAsync<List<Document>>(FAQ_INDEX_CACHE);
                stats.CacheHitCount = allDocs != null ? 1 : 0;
                stats.CacheMissCount = stats.CacheHitCount == 0 ? 1 : 0;
                stats.CacheHitRate = stats.CacheHitCount + stats.CacheMissCount > 0
                    ? (double)stats.CacheHitCount / (stats.CacheHitCount + stats.CacheMissCount)
                    : 0;

                var allFaqs = await GetAllFaqsAsync(cancellationToken);
                stats.CategoryCounts = allFaqs
                    .GroupBy(f => f.Category)
                    .ToDictionary(g => g.Key, g => g.Count());

                stats.LanguageCounts = allFaqs
                    .GroupBy(f => f.Language)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cache stats");
            }

            return stats;
        }

        // ✅ JSON Models for deserialization
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