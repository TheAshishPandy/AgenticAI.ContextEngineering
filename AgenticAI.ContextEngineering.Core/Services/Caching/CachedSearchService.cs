using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    /// <summary>
    /// Search service with multi-level caching
    /// </summary>
    public class CachedSearchService
    {
        private readonly LexicalSearch _lexicalSearch;
        private readonly SemanticSearch _semanticSearch;
        private readonly HybridSearchEngine _hybridSearchEngine;
        private readonly IKVCache _cache;
        private readonly ILogger<CachedSearchService> _logger;

        private const string SEARCH_CACHE_PREFIX = "search:";
        private const string LEXICAL_CACHE_PREFIX = "lexical:";
        private const string SEMANTIC_CACHE_PREFIX = "semantic:";
        private const string HYBRID_CACHE_PREFIX = "hybrid:";

        public CachedSearchService(
            LexicalSearch lexicalSearch,
            SemanticSearch semanticSearch,
            HybridSearchEngine hybridSearchEngine,
            IKVCache cache,
            ILogger<CachedSearchService> logger)
        {
            _lexicalSearch = lexicalSearch;
            _semanticSearch = semanticSearch;
            _hybridSearchEngine = hybridSearchEngine;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Cached Lexical Search
        /// </summary>
        public async Task<SearchResponse> LexicalSearchAsync(
            SearchRequest request,
            bool useCache = true)
        {
            if (!useCache)
                return await _lexicalSearch.SearchAsync(request);

            var cacheKey = $"{LEXICAL_CACHE_PREFIX}{request.Query.GetHashCode()}_{request.TopResults}";

            // ✅ Try get from cache
            var cached = await _cache.GetAsync<SearchResponse>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"✅ Lexical search cache HIT for: {request.Query}");
                return cached;
            }

            _logger.LogInformation($"❌ Lexical search cache MISS for: {request.Query}");

            // ✅ Execute search
            var response = await _lexicalSearch.SearchAsync(request);

            // ✅ Cache the result
            if (response != null && response.Results.Count > 0)
            {
                await _cache.SetAsync(cacheKey, response, TimeSpan.FromHours(1));
                _logger.LogInformation($"✅ Lexical search cached for: {request.Query}");
            }

            return response;
        }

        /// <summary>
        /// Cached Semantic Search
        /// </summary>
        public async Task<SearchResponse> SemanticSearchAsync(
            SearchRequest request,
            float[] queryVector,
            bool useCache = true)
        {
            if (!useCache)
                return await _semanticSearch.SearchAsync(request, queryVector);

            var cacheKey = $"{SEMANTIC_CACHE_PREFIX}{request.Query.GetHashCode()}_{request.TopResults}";

            var cached = await _cache.GetAsync<SearchResponse>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"✅ Semantic search cache HIT for: {request.Query}");
                return cached;
            }

            _logger.LogInformation($"❌ Semantic search cache MISS for: {request.Query}");

            var response = await _semanticSearch.SearchAsync(request, queryVector);

            if (response != null && response.Results.Count > 0)
            {
                await _cache.SetAsync(cacheKey, response, TimeSpan.FromHours(1));
                _logger.LogInformation($"✅ Semantic search cached for: {request.Query}");
            }

            return response;
        }

        /// <summary>
        /// Cached Hybrid Search
        /// </summary>
        public async Task<SearchResponse> HybridSearchAsync(
            SearchRequest request,
            bool useCache = true)
        {
            if (!useCache)
                return await _hybridSearchEngine.HybridSearchAsync(request);

            var cacheKey = $"{HYBRID_CACHE_PREFIX}{request.Query.GetHashCode()}_{request.TopResults}";

            var cached = await _cache.GetAsync<SearchResponse>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation($"✅ Hybrid search cache HIT for: {request.Query}");
                return cached;
            }

            _logger.LogInformation($"❌ Hybrid search cache MISS for: {request.Query}");

            var response = await _hybridSearchEngine.HybridSearchAsync(request);

            if (response != null && response.Results.Count > 0)
            {
                await _cache.SetAsync(cacheKey, response, TimeSpan.FromHours(1));
                _logger.LogInformation($"✅ Hybrid search cached for: {request.Query}");
            }

            return response;
        }

        /// <summary>
        /// Multi-Query Search with Caching
        /// </summary>
        public async Task<Dictionary<string, SearchResponse>> MultiSearchAsync(
            List<string> queries,
            SearchType searchType = SearchType.Hybrid,
            int topResults = 5)
        {
            var results = new Dictionary<string, SearchResponse>();
            var tasks = new List<Task<KeyValuePair<string, SearchResponse>>>();

            foreach (var query in queries)
            {
                tasks.Add(SearchSingleQueryAsync(query, searchType, topResults));
            }

            var completed = await Task.WhenAll(tasks);
            foreach (var result in completed)
            {
                results[result.Key] = result.Value;
            }

            return results;
        }

        private async Task<KeyValuePair<string, SearchResponse>> SearchSingleQueryAsync(
            string query,
            SearchType searchType,
            int topResults)
        {
            var request = new SearchRequest
            {
                Query = query,
                TopResults = topResults,
                IncludeScoreBreakdown = true
            };

            SearchResponse response = searchType switch
            {
                SearchType.Lexical => await LexicalSearchAsync(request),
                SearchType.Semantic => await SemanticSearchAsync(request, null),
                _ => await HybridSearchAsync(request)
            };

            return new KeyValuePair<string, SearchResponse>(query, response);
        }
    }
}