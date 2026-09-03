// AgenticAI.ContextEngineering.Core/Embeddings/RateLimitedEmbeddingGenerator.cs
using AgenticAI.ContextEngineering.Core.Exceptions;
using AgenticAI.ContextEngineering.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Embeddings
{
    /// <summary>
    /// Decorator that implements rate limiting for embedding generation API calls.
    /// Uses a token bucket algorithm to prevent API rate limit rejections.
    /// </summary>
    public class RateLimitedEmbeddingGenerator : IEmbeddingGenerator
    {
        private readonly IEmbeddingGenerator _inner;
        private readonly ILogger<RateLimitedEmbeddingGenerator> _logger;
        private readonly SemaphoreSlim _concurrencyLimiter;

        // Configuration
        private const int DEFAULT_CONCURRENT_REQUESTS = 5;
        private const int DEFAULT_REQUESTS_PER_MINUTE = 100;  // Typically 100/min per OpenAI standard tier
        private const int TOKEN_BUCKET_WINDOW_SECONDS = 60;

        private readonly int _maxConcurrentRequests;
        private readonly int _maxRequestsPerMinute;

        // Token bucket tracking
        private readonly object _lockObject = new object();
        private Queue<DateTime> _requestTimestamps = new Queue<DateTime>();
        private DateTime _lastLoggedWarning = DateTime.MinValue;
        private const int WARNING_THRESHOLD_PERCENTAGE = 80;  // Warn at 80% capacity

        /// <summary>
        /// Gets the number of dimensions in the embedding vector.
        /// </summary>
        public int Dimensions => _inner.Dimensions;

        /// <summary>
        /// Gets whether the embedding generator is enabled.
        /// </summary>
        public bool IsEnabled => _inner.IsEnabled;

        /// <summary>
        /// Initializes a new instance of the RateLimitedEmbeddingGenerator class.
        /// </summary>
        public RateLimitedEmbeddingGenerator(
            IEmbeddingGenerator inner,
            ILogger<RateLimitedEmbeddingGenerator> logger,
            int? maxConcurrentRequests = null,
            int? maxRequestsPerMinute = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _maxConcurrentRequests = maxConcurrentRequests ?? DEFAULT_CONCURRENT_REQUESTS;
            _maxRequestsPerMinute = maxRequestsPerMinute ?? DEFAULT_REQUESTS_PER_MINUTE;

            _concurrencyLimiter = new SemaphoreSlim(_maxConcurrentRequests, _maxConcurrentRequests);

            _logger.LogInformation(
                "RateLimitedEmbeddingGenerator initialized with {MaxConcurrent} concurrent requests and {MaxPerMinute} requests/minute",
                _maxConcurrentRequests,
                _maxRequestsPerMinute);
        }

        /// <summary>
        /// Generates a single embedding with rate limiting.
        /// </summary>
        public async Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentNullException(nameof(text));

            // Acquire concurrency slot
            bool acquired = await _concurrencyLimiter.WaitAsync(
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (!acquired)
            {
                _logger.LogError("Failed to acquire concurrency slot within 30 seconds");
                throw new OperationTimeoutException("Embedding generation", 30000);
            }

            try
            {
                // Wait for rate limit token to be available
                await EnsureRateLimitAsync();

                // Call the inner generator
                var embedding = await _inner.GenerateEmbeddingAsync(text, cancellationToken);

                return embedding;
            }
            catch (RateLimitExceededException)
            {
                _logger.LogWarning("Rate limit exceeded during embedding generation");
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Embedding generation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text of length {TextLength}", text.Length);
                throw new EmbeddingGenerationException(text, ex);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        /// <summary>
        /// Generates multiple embeddings with rate limiting.
        /// </summary>
        public async Task<List<float[]>> GenerateEmbeddingsAsync(
            List<string> texts,
            CancellationToken cancellationToken = default)
        {
            if (texts == null || texts.Count == 0)
                throw new ArgumentException("Texts collection cannot be empty", nameof(texts));

            var results = new List<float[]>();

            // Process sequentially to maintain rate limit
            foreach (var text in texts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
                results.Add(embedding);
            }

            return results;
        }

        /// <summary>
        /// Ensures rate limit token is available before proceeding.
        /// Uses sliding window token bucket algorithm.
        /// </summary>
        private async Task EnsureRateLimitAsync()
        {
            while (true)
            {
                lock (_lockObject)
                {
                    var now = DateTime.UtcNow;

                    // Remove timestamps outside the sliding window
                    while (_requestTimestamps.Count > 0)
                    {
                        var oldest = _requestTimestamps.Peek();
                        if ((now - oldest).TotalSeconds > TOKEN_BUCKET_WINDOW_SECONDS)
                        {
                            _requestTimestamps.Dequeue();
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Check if we're within rate limit
                    if (_requestTimestamps.Count < _maxRequestsPerMinute)
                    {
                        // Check for approaching limit (80% full)
                        int percentFull = (_requestTimestamps.Count * 100) / _maxRequestsPerMinute;
                        if (percentFull >= WARNING_THRESHOLD_PERCENTAGE)
                        {
                            if ((now - _lastLoggedWarning).TotalSeconds > 10)  // Log warning at most every 10 seconds
                            {
                                _logger.LogWarning(
                                    "Rate limit approaching: {Current}/{Max} ({Percentage}%)",
                                    _requestTimestamps.Count,
                                    _maxRequestsPerMinute,
                                    percentFull);
                                _lastLoggedWarning = now;
                            }
                        }

                        // Add request timestamp
                        _requestTimestamps.Enqueue(now);
                        return;  // Within limit, proceed
                    }

                    // At limit, calculate delay
                    var oldestRequest = _requestTimestamps.Peek();
                    var ageOfOldest = (now - oldestRequest).TotalSeconds;
                    var delayNeeded = TOKEN_BUCKET_WINDOW_SECONDS - ageOfOldest;

                    if (delayNeeded > 0)
                    {
                        _logger.LogDebug(
                            "Rate limit reached. Current: {Current}/{Max}. Delaying {DelayMs}ms",
                            _requestTimestamps.Count,
                            _maxRequestsPerMinute,
                            (int)(delayNeeded * 1000));

                        // Release lock before sleeping
                    }
                }

                // Rate limit exceeded, wait and retry
                var delayMs = 100;  // Check every 100ms
                await Task.Delay(delayMs);
            }
        }

        /// <summary>
        /// Gets current rate limit statistics.
        /// </summary>
        public (int CurrentRequests, int MaxRequests, int CapacityPercent) GetRateLimitStats()
        {
            lock (_lockObject)
            {
                var now = DateTime.UtcNow;

                // Clean old timestamps
                while (_requestTimestamps.Count > 0)
                {
                    var oldest = _requestTimestamps.Peek();
                    if ((now - oldest).TotalSeconds > TOKEN_BUCKET_WINDOW_SECONDS)
                    {
                        _requestTimestamps.Dequeue();
                    }
                    else
                    {
                        break;
                    }
                }

                int percent = (_requestTimestamps.Count * 100) / _maxRequestsPerMinute;
                return (_requestTimestamps.Count, _maxRequestsPerMinute, percent);
            }
        }

        /// <summary>
        /// Resets rate limit tracking (useful for testing).
        /// </summary>
        public void ResetRateLimit()
        {
            lock (_lockObject)
            {
                _requestTimestamps.Clear();
                _logger.LogInformation("Rate limit tracking reset");
            }
        }
    }
}
