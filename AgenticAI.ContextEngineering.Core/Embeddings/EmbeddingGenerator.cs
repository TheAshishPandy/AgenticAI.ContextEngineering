// AgenticAI.ContextEngineering.Core/Embeddings/EmbeddingGenerator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.AI.OpenAI;
using System.ClientModel;

namespace AgenticAI.ContextEngineering.Core.Embeddings
{
    /// <summary>
    /// Embedding generator using Azure OpenAI with caching and batch processing
    /// </summary>
    public class EmbeddingGenerator : IEmbeddingGenerator
    {
        private readonly ILogger<EmbeddingGenerator> _logger;
        private readonly AzureOpenAIClient? _openAIClient;
        private readonly IKVCache? _kvCache;
        private readonly string _model;
        private readonly int _dimensions;
        private readonly bool _isEnabled;
        private readonly int _batchSize;
        private readonly int _retryCount;
        private readonly int _retryDelayMs;
        private readonly bool _enableCaching;

        // Cache prefix for embeddings
        private const string EMBEDDING_CACHE_PREFIX = "embedding:";
        private const int DEFAULT_BATCH_SIZE = 10;
        private const int DEFAULT_RETRY_COUNT = 3;
        private const int DEFAULT_RETRY_DELAY_MS = 1000;

        public int Dimensions => _dimensions;
        public bool IsEnabled => _isEnabled;

        public EmbeddingGenerator(
            IConfiguration configuration,
            ILogger<EmbeddingGenerator> logger,
            IKVCache? kvCache = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kvCache = kvCache;

            // Load configuration
            var endpoint = configuration["AzureOpenAIEmbedding:Endpoint"];
            var apiKey = configuration["AzureOpenAIEmbedding:ApiKey"];
            _model = configuration["AzureOpenAIEmbedding:EmbeddingModel"] ?? "text-embedding-ada-002";
            _dimensions = configuration.GetValue<int>("AzureOpenAIEmbedding:EmbeddingDimensions", 1536);
            _isEnabled = configuration.GetValue<bool>("AzureOpenAIEmbedding:EmbeddingEnabled", true);
            _batchSize = configuration.GetValue<int>("AzureOpenAIEmbedding:EmbeddingBatchSize", DEFAULT_BATCH_SIZE);
            _retryCount = configuration.GetValue<int>("AzureOpenAIEmbedding:EmbeddingRetryCount", DEFAULT_RETRY_COUNT);
            _retryDelayMs = configuration.GetValue<int>("AzureOpenAIEmbedding:EmbeddingRetryDelayMs", DEFAULT_RETRY_DELAY_MS);
            _enableCaching = configuration.GetValue<bool>("AzureOpenAIEmbedding:EmbeddingCacheEnabled", true);

            try
            {
                if (_isEnabled && !string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
                {
                    // ✅ FIX: ApiKeyCredential expects a string, not byte[]
                    _openAIClient = new AzureOpenAIClient(
                        new Uri(endpoint),
                        new ApiKeyCredential(apiKey));
                    _logger.LogInformation($"✅ EmbeddingGenerator initialized with model: {_model}, dimensions: {_dimensions}");
                }
                else if (_isEnabled)
                {
                    _logger.LogWarning("⚠️ Azure OpenAI credentials missing. Embedding generation will be disabled.");
                    _isEnabled = false;
                }
                else
                {
                    _logger.LogInformation("ℹ️ Embedding generation is disabled by configuration");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize EmbeddingGenerator");
                _isEnabled = false;
            }
        }

        #region ============================================================
        // Generate Embedding - Single
        // ============================================================

        public async Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            if (!_isEnabled)
            {
                _logger.LogWarning("Embedding generation is disabled");
                return Array.Empty<float>();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogDebug("Empty text received, returning empty embedding");
                return Array.Empty<float>();
            }

            // Check cache first
            if (_enableCaching && _kvCache != null)
            {
                var cacheKey = $"{EMBEDDING_CACHE_PREFIX}{text.GetHashCode()}";
                try
                {
                    var cached = await _kvCache.GetAsync<float[]>(cacheKey);
                    if (cached != null && cached.Length > 0)
                    {
                        _logger.LogDebug($"✅ Embedding cache HIT for: '{text.Substring(0, Math.Min(50, text.Length))}'");
                        return cached;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read embedding from cache");
                }
            }

            // Generate embedding with retry
            for (int attempt = 1; attempt <= _retryCount; attempt++)
            {
                try
                {
                    if (_openAIClient == null)
                    {
                        _logger.LogError("Azure OpenAI client is not initialized");
                        return Array.Empty<float>();
                    }

                    _logger.LogDebug($"🔄 Generating embedding (attempt {attempt}) for: '{text.Substring(0, Math.Min(50, text.Length))}'");

                    var embeddingClient = _openAIClient.GetEmbeddingClient(_model);
                    var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);

                    if (response?.Value == null)
                    {
                        _logger.LogWarning("Null response from Azure OpenAI");
                        if (attempt < _retryCount)
                        {
                            await Task.Delay(_retryDelayMs * attempt, cancellationToken);
                            continue;
                        }
                        return Array.Empty<float>();
                    }

                    var embedding = response.Value.ToFloats().ToArray();

                    if (embedding == null || embedding.Length == 0)
                    {
                        _logger.LogWarning("Empty embedding received");
                        if (attempt < _retryCount)
                        {
                            await Task.Delay(_retryDelayMs * attempt, cancellationToken);
                            continue;
                        }
                        return Array.Empty<float>();
                    }

                    // Cache the embedding
                    if (_enableCaching && _kvCache != null && embedding.Length > 0)
                    {
                        try
                        {
                            var cacheKey = $"{EMBEDDING_CACHE_PREFIX}{text.GetHashCode()}";
                            await _kvCache.SetAsync(cacheKey, embedding, TimeSpan.FromHours(24));
                            _logger.LogDebug($"✅ Embedding cached for: '{text.Substring(0, Math.Min(50, text.Length))}'");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to cache embedding");
                        }
                    }

                    _logger.LogDebug($"✅ Embedding generated: {embedding.Length} dimensions");
                    return embedding;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Embedding generation cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Failed to generate embedding (attempt {attempt}): {ex.Message}");
                    if (attempt < _retryCount)
                    {
                        _logger.LogDebug($"⏳ Retrying in {_retryDelayMs * attempt}ms...");
                        await Task.Delay(_retryDelayMs * attempt, cancellationToken);
                    }
                }
            }

            _logger.LogError($"❌ Failed to generate embedding after {_retryCount} attempts");
            return Array.Empty<float>();
        }

        #endregion

        #region ============================================================
        // Generate Embeddings - Batch
        // ============================================================

        public async Task<List<float[]>> GenerateEmbeddingsAsync(
            List<string> texts,
            CancellationToken cancellationToken = default)
        {
            if (!_isEnabled)
            {
                _logger.LogWarning("Embedding generation is disabled");
                return new List<float[]>();
            }

            if (texts == null || !texts.Any())
            {
                return new List<float[]>();
            }

            _logger.LogDebug($"🔄 Generating embeddings for {texts.Count} texts");

            var results = new List<float[]>();
            var filteredTexts = texts.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

            if (!filteredTexts.Any())
            {
                return new List<float[]>();
            }

            // Process in batches
            for (int i = 0; i < filteredTexts.Count; i += _batchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Embedding generation cancelled");
                    break;
                }

                var batch = filteredTexts.Skip(i).Take(_batchSize).ToList();
                var batchResults = await GenerateEmbeddingsBatchAsync(batch, cancellationToken);
                results.AddRange(batchResults);

                // Add delay between batches to avoid rate limiting
                if (i + _batchSize < filteredTexts.Count)
                {
                    await Task.Delay(200, cancellationToken);
                }
            }

            _logger.LogDebug($"✅ Generated {results.Count} embeddings");
            return results;
        }

        private async Task<List<float[]>> GenerateEmbeddingsBatchAsync(
            List<string> texts,
            CancellationToken cancellationToken)
        {
            var results = new List<float[]>();

            if (texts == null || !texts.Any())
                return results;

            // Check cache for each text first
            var uncachedTexts = new List<string>();
            var cachedResults = new Dictionary<string, float[]>();

            if (_enableCaching && _kvCache != null)
            {
                foreach (var text in texts)
                {
                    try
                    {
                        var cacheKey = $"{EMBEDDING_CACHE_PREFIX}{text.GetHashCode()}";
                        var cached = await _kvCache.GetAsync<float[]>(cacheKey);
                        if (cached != null && cached.Length > 0)
                        {
                            cachedResults[text] = cached;
                            _logger.LogDebug($"✅ Embedding cache HIT for: '{text.Substring(0, Math.Min(50, text.Length))}'");
                        }
                        else
                        {
                            uncachedTexts.Add(text);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read embedding from cache");
                        uncachedTexts.Add(text);
                    }
                }
            }
            else
            {
                uncachedTexts = texts;
            }

            // Generate embeddings for uncached texts
            var generatedResults = new Dictionary<string, float[]>();

            if (uncachedTexts.Any())
            {
                _logger.LogDebug($"🔄 Generating {uncachedTexts.Count} embeddings");

                var tasks = uncachedTexts.Select(text => GenerateEmbeddingWithRetryAsync(text, cancellationToken));
                var resultsArray = await Task.WhenAll(tasks);

                for (int i = 0; i < uncachedTexts.Count; i++)
                {
                    generatedResults[uncachedTexts[i]] = resultsArray[i] ?? Array.Empty<float>();
                }
            }

            // Combine cached and generated results in original order
            foreach (var text in texts)
            {
                if (cachedResults.ContainsKey(text))
                {
                    results.Add(cachedResults[text]);
                }
                else if (generatedResults.ContainsKey(text))
                {
                    var embedding = generatedResults[text];
                    results.Add(embedding);

                    // Cache generated embedding
                    if (_enableCaching && _kvCache != null && embedding.Length > 0)
                    {
                        try
                        {
                            var cacheKey = $"{EMBEDDING_CACHE_PREFIX}{text.GetHashCode()}";
                            await _kvCache.SetAsync(cacheKey, embedding, TimeSpan.FromHours(24));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to cache embedding");
                        }
                    }
                }
                else
                {
                    results.Add(Array.Empty<float>());
                }
            }

            return results;
        }

        private async Task<float[]> GenerateEmbeddingWithRetryAsync(
            string text,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<float>();

            for (int attempt = 1; attempt <= _retryCount; attempt++)
            {
                try
                {
                    if (_openAIClient == null)
                        return Array.Empty<float>();

                    var embeddingClient = _openAIClient.GetEmbeddingClient(_model);
                    var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);

                    if (response?.Value == null)
                    {
                        if (attempt < _retryCount)
                        {
                            await Task.Delay(_retryDelayMs * attempt, cancellationToken);
                            continue;
                        }
                        return Array.Empty<float>();
                    }

                    var embedding = response.Value.ToFloats().ToArray();

                    if (embedding == null || embedding.Length == 0)
                    {
                        if (attempt < _retryCount)
                        {
                            await Task.Delay(_retryDelayMs * attempt, cancellationToken);
                            continue;
                        }
                        return Array.Empty<float>();
                    }

                    return embedding;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to generate embedding (attempt {attempt})");
                    if (attempt < _retryCount)
                    {
                        await Task.Delay(_retryDelayMs * attempt, cancellationToken);
                    }
                }
            }

            return Array.Empty<float>();
        }

        #endregion

        #region ============================================================
        // Cache Management
        // ============================================================

        public async Task ClearEmbeddingCacheAsync()
        {
            if (_kvCache != null)
            {
                try
                {
                    await _kvCache.ClearAsync();
                    _logger.LogInformation("🗑️ Embedding cache cleared");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clear embedding cache");
                }
            }
        }

        public async Task<Dictionary<string, object>> GetCacheStatsAsync()
        {
            var stats = new Dictionary<string, object>
            {
                ["IsEnabled"] = _isEnabled,
                ["Model"] = _model,
                ["Dimensions"] = _dimensions,
                ["BatchSize"] = _batchSize,
                ["RetryCount"] = _retryCount,
                ["CacheEnabled"] = _enableCaching
            };

            if (_kvCache != null)
            {
                try
                {
                    var cacheStats = _kvCache.GetStatistics();
                    stats["CacheStats"] = cacheStats;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get cache statistics");
                }
            }

            return stats;
        }

        #endregion
    }
}