// SmartChatBot.ContextEngineering.Demo/Services/DemoService.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Services
{
    public class DemoService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DemoService> _logger;

        public DemoService(
            IServiceProvider serviceProvider,
            ILogger<DemoService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        #region Test 1: AI Response with Token Caching

        public async Task TestAIResponseWithCachingAsync()
        {
            Console.WriteLine("🤖 TEST: AI Response with Token Caching");
            Console.WriteLine(new string('=', 60));

            try
            {
                var aiService = _serviceProvider.GetService<IAIResponseService>();
                if (aiService == null)
                {
                    Console.WriteLine("  ❌ IAIResponseService not registered");
                    return;
                }

                var tokenCache = _serviceProvider.GetService<ITokenCache>();
                if (tokenCache == null)
                {
                    Console.WriteLine("  ❌ ITokenCache not registered");
                    return;
                }

                var queries = new[]
                {
                    "What is artificial intelligence?",
                    "Explain machine learning in simple terms.",
                    "What is the difference between AI and ML?"
                };

                foreach (var query in queries)
                {
                    Console.WriteLine($"\n  📝 Query: {query}");

                    // ✅ First call - should be a cache MISS
                    Console.WriteLine("    🔄 First call (should be cache MISS)...");
                    var stopwatch = Stopwatch.StartNew();

                    var request = new AIResponseRequest
                    {
                        Query = query,
                        UserQuery = query,
                        Temperature = 0.7f,
                        MaxTokens = 200,
                        UseCache = true,
                        SystemPrompt = "You are a helpful assistant. Provide concise answers."
                    };

                    var response1 = await aiService.GenerateResponseAsync(request);
                    stopwatch.Stop();

                    var fromCache1 = response1.FromCache ? "Yes" : "No";
                    var preview1 = response1.Response?.Length > 100 ? response1.Response.Substring(0, 100) + "..." : response1.Response;

                    Console.WriteLine($"      ✅ Response: {preview1}");
                    Console.WriteLine($"      ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"      💾 From Cache: {fromCache1}");
                    Console.WriteLine($"      📊 Tokens: {response1.TokenCount}");

                    // ✅ Second call - should be a cache HIT
                    Console.WriteLine($"    🔄 Second call (should be cache HIT)...");
                    stopwatch.Restart();

                    var response2 = await aiService.GenerateResponseAsync(request);
                    stopwatch.Stop();

                    var fromCache2 = response2.FromCache ? "Yes" : "No";

                    Console.WriteLine($"      ✅ Response: {(response2.Response?.Length > 100 ? response2.Response.Substring(0, 100) + "..." : response2.Response)}");
                    Console.WriteLine($"      ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"      💾 From Cache: {fromCache2}");
                    Console.WriteLine($"      📊 Tokens: {response2.TokenCount}");

                    // ✅ Check if caching worked
                    if (fromCache1 == "No" && fromCache2 == "Yes")
                    {
                        Console.WriteLine($"      ✅ Caching WORKED! ({(stopwatch.ElapsedMilliseconds < 50 ? "Fast response" : "Cache hit")})");
                    }
                    else if (fromCache1 == "No" && fromCache2 == "No")
                    {
                        Console.WriteLine($"      ⚠️ Caching may not be working (both calls were cache MISS)");
                    }
                }

                // ✅ Get token stats
                var stats = tokenCache.GetTokenStats();
                Console.WriteLine($"\n  📊 Token Cache Statistics:");
                Console.WriteLine($"    Total Tokens Cached: {stats.TotalTokensCached:N0}");
                Console.WriteLine($"    Total Tokens Saved: {stats.TotalTokensSaved:N0}");
                Console.WriteLine($"    Cache Hit Rate: {stats.CacheHitRate:P2}");
                Console.WriteLine($"    Cost Saved: ${stats.CostSaved:F2}");

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"     Inner: {ex.InnerException.Message}");
                }
            }
        }

        #endregion

        #region Test 2: Token Optimized Service

        public async Task TestTokenOptimizedServiceAsync()
        {
            Console.WriteLine("💰 TEST: Token Optimized Service");
            Console.WriteLine(new string('=', 60));

            try
            {
                var tokenService = _serviceProvider.GetService<ITokenOptimizedService>();
                if (tokenService == null)
                {
                    Console.WriteLine("  ❌ ITokenOptimizedService not registered");
                    return;
                }

                var queries = new[]
                {
                    "Explain quantum computing in simple terms.",
                    "What is the capital of France?",
                    "Tell me about renewable energy."
                };

                foreach (var query in queries)
                {
                    var options = new OptimizedRequestOptions
                    {
                        SystemPrompt = "You are a helpful assistant. Give brief answers.",
                        MaxTokens = 150,
                        Temperature = 0.5f,
                        UseCache = true,
                        Metadata = new Dictionary<string, string>
                        {
                            ["Category"] = "General Knowledge",
                            ["Priority"] = "High"
                        }
                    };

                    Console.WriteLine($"\n  📝 Query: {query}");

                    var stopwatch = Stopwatch.StartNew();
                    var response = await tokenService.GetOptimizedResponseAsync(query, options);
                    stopwatch.Stop();

                    if (response.IsSuccess)
                    {
                        var preview = response.Answer?.Length > 80 ? response.Answer.Substring(0, 80) + "..." : response.Answer;
                        Console.WriteLine($"    ✅ Answer: {preview}");
                        Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                        Console.WriteLine($"    💾 From Cache: {(response.FromCache ? "Yes" : "No")}");
                        Console.WriteLine($"    📊 Confidence: {response.Confidence:F2}");
                    }
                    else
                    {
                        Console.WriteLine($"    ❌ Error: {response.Error}");
                    }
                }

                var stats = tokenService.GetTokenStats();
                Console.WriteLine($"\n  📊 Token Stats:");
                Console.WriteLine($"    Total Tokens Saved: {stats.TotalTokensSaved:N0}");
                Console.WriteLine($"    Cache Hit Rate: {stats.CacheHitRate:P2}");
                Console.WriteLine($"    Cost Saved: ${stats.CostSaved:F2}");

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Test 3: Token Cache Stats

        public async Task TestTokenCacheStatsAsync()
        {
            Console.WriteLine("📊 TEST: Token Cache Statistics");
            Console.WriteLine(new string('=', 60));

            try
            {
                var tokenCache = _serviceProvider.GetService<ITokenCache>();
                if (tokenCache == null)
                {
                    Console.WriteLine("  ❌ ITokenCache not registered");
                    return;
                }

                var stats = tokenCache.GetTokenStats();

                Console.WriteLine($"\n  📊 Token Cache Statistics:");
                Console.WriteLine($"    Total Tokens Cached: {stats.TotalTokensCached:N0}");
                Console.WriteLine($"    Total Tokens Saved: {stats.TotalTokensSaved:N0}");
                Console.WriteLine($"    Total Prompts Cached: {stats.TotalPromptsCached:N0}");
                Console.WriteLine($"    Total Completions Cached: {stats.TotalCompletionsCached:N0}");
                Console.WriteLine($"    Total Embeddings Cached: {stats.TotalEmbeddingsCached:N0}");
                Console.WriteLine($"    Cache Hit Rate: {stats.CacheHitRate:P2}");
                Console.WriteLine($"    Cost Saved: ${stats.CostSaved:F2}");
                Console.WriteLine($"    Last Updated: {stats.StatsUpdated:yyyy-MM-dd HH:mm:ss} UTC");

                Console.WriteLine("\n  📈 Token Savings by Type:");
                foreach (var kvp in stats.TokenSavingsByType)
                {
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value:N0} tokens");
                }

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Test 4: Clear Token Cache

        public async Task TestClearTokenCacheAsync()
        {
            Console.WriteLine("🗑️ TEST: Clear Token Cache");
            Console.WriteLine(new string('=', 60));

            try
            {
                var tokenCache = _serviceProvider.GetService<ITokenCache>();
                if (tokenCache == null)
                {
                    Console.WriteLine("  ❌ ITokenCache not registered");
                    return;
                }

                var statsBefore = tokenCache.GetTokenStats();
                Console.WriteLine($"\n  📊 Stats Before Clear:");
                Console.WriteLine($"    Total Tokens Cached: {statsBefore.TotalTokensCached:N0}");
                Console.WriteLine($"    Total Tokens Saved: {statsBefore.TotalTokensSaved:N0}");

                await tokenCache.ClearAsync();
                Console.WriteLine($"\n  ✅ Token cache cleared!");

                var statsAfter = tokenCache.GetTokenStats();
                Console.WriteLine($"\n  📊 Stats After Clear:");
                Console.WriteLine($"    Total Tokens Cached: {statsAfter.TotalTokensCached:N0}");
                Console.WriteLine($"    Total Tokens Saved: {statsAfter.TotalTokensSaved:N0}");

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Test 5: Comparison - With and Without Cache

        public async Task TestCacheComparisonAsync()
        {
            Console.WriteLine("⚡ TEST: Cache Performance Comparison");
            Console.WriteLine(new string('=', 60));

            try
            {
                var aiService = _serviceProvider.GetService<IAIResponseService>();
                if (aiService == null)
                {
                    Console.WriteLine("  ❌ IAIResponseService not registered");
                    return;
                }

                var query = "What is the meaning of life?";
                Console.WriteLine($"\n  📝 Query: {query}");

                // ✅ Test 1: Without Cache
                Console.WriteLine("\n  🔄 Test 1: Without Cache...");
                var request = new AIResponseRequest
                {
                    Query = query,
                    UserQuery = query,
                    UseCache = false,
                    Temperature = 0.7f,
                    MaxTokens = 200
                };

                var stopwatch = Stopwatch.StartNew();
                var responseNoCache = await aiService.GenerateResponseAsync(request);
                stopwatch.Stop();

                Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"    📊 Tokens: {responseNoCache.TokenCount}");

                // ✅ Test 2: With Cache (First call - MISS)
                Console.WriteLine("\n  🔄 Test 2: With Cache (First call - should be MISS)...");
                request.UseCache = true;
                stopwatch.Restart();
                var responseWithCache1 = await aiService.GenerateResponseAsync(request);
                stopwatch.Stop();

                Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"    📊 Tokens: {responseWithCache1.TokenCount}");
                Console.WriteLine($"    💾 From Cache: {responseWithCache1.FromCache}");

                // ✅ Test 3: With Cache (Second call - HIT)
                Console.WriteLine("\n  🔄 Test 3: With Cache (Second call - should be HIT)...");
                stopwatch.Restart();
                var responseWithCache2 = await aiService.GenerateResponseAsync(request);
                stopwatch.Stop();

                Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"    📊 Tokens: {responseWithCache2.TokenCount}");
                Console.WriteLine($"    💾 From Cache: {responseWithCache2.FromCache}");

                // ✅ Performance comparison
                Console.WriteLine($"\n  📊 Performance Comparison:");
                Console.WriteLine($"    Without Cache: {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"    With Cache (Hit): {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"    Speed Improvement: {(responseNoCache.ProcessingTimeMs > 0 && responseWithCache2.ProcessingTimeMs > 0 ?
                    $"{((responseNoCache.ProcessingTimeMs - responseWithCache2.ProcessingTimeMs) / (double)responseNoCache.ProcessingTimeMs * 100):F0}% faster" : "N/A")}");

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        public async Task TestKVCacheDirectlyAsync()
        {
            Console.WriteLine("🗄️ TEST: KV Cache Direct Access");
            Console.WriteLine(new string('=', 60));

            try
            {
                var kvCache = _serviceProvider.GetService<IKVCache>();
                if (kvCache == null)
                {
                    Console.WriteLine("  ❌ IKVCache not registered");
                    return;
                }

                // ✅ Test Set
                var testKey = "test:ai:response:hello";
                var testValue = "Hello, this is a test cached value!";

                Console.WriteLine($"  📝 Setting cache: {testKey}");
                await kvCache.SetAsync(testKey, testValue, TimeSpan.FromMinutes(5));

                // ✅ Test Get
                Console.WriteLine($"  🔍 Getting cache: {testKey}");
                var retrieved = await kvCache.GetAsync<string>(testKey);

                Console.WriteLine($"  ✅ Retrieved: {retrieved != null}");
                if (retrieved != null)
                {
                    Console.WriteLine($"     Value: {retrieved}");
                }

                // ✅ Test Remove
                Console.WriteLine($"  🗑️ Removing cache: {testKey}");
                await kvCache.RemoveAsync(testKey);

                var afterRemove = await kvCache.GetAsync<string>(testKey);
                Console.WriteLine($"  ✅ After Remove: {(afterRemove == null ? "Not found (correct)" : "Found (incorrect)")}");

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion
    }
}