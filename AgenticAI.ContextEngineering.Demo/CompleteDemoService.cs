// AgenticAI.ContextEngineering.Demo/Services/CompleteDemoService.cs
using AgenticAI.ContextEngineering.Core.Caching;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Services
{
    public class CompleteDemoService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CompleteDemoService> _logger;

        public CompleteDemoService(
            IServiceProvider serviceProvider,
            ILogger<CompleteDemoService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task RunAllDemosAsync()
        {
            Console.WriteLine("\n" + new string('═', 70));
            Console.WriteLine("  🚀 AGENTICAI CONTEXT ENGINEERING - COMPLETE DEMO");
            Console.WriteLine(new string('═', 70));

            await DemoKVCacheAsync();
            await DemoTokenCacheAsync();
            await DemoAIResponseWithCachingAsync();
            await DemoSearchAsync();
            await DemoFAQServiceAsync();
            await DemoTokenOptimizedServiceAsync();
            await DemoCacheStatisticsAsync();
            await DemoClearCacheAsync();

            Console.WriteLine("\n" + new string('═', 70));
            Console.WriteLine("  ✅ ALL DEMOS COMPLETED SUCCESSFULLY!");
            Console.WriteLine(new string('═', 70));
        }

        #region Demo 1: KV Cache

        public async Task DemoKVCacheAsync()
        {
            Console.WriteLine("\n📦 DEMO 1: KV Cache");
            Console.WriteLine(new string('─', 60));

            try
            {
                var kvCache = _serviceProvider.GetService<IKVCache>();
                if (kvCache == null)
                {
                    Console.WriteLine("  ❌ IKVCache not registered");
                    return;
                }

                Console.WriteLine($"  📋 Cache Type: {kvCache.GetType().Name}");
                Console.WriteLine($"  📋 Cache Name: {kvCache.Name}");
                Console.WriteLine($"  📋 Initial Count: {kvCache.Count}");

                // Test Set
                var testKey = "demo:kv:test:1";
                var testValue = new { Id = 1, Name = "Test Item", Timestamp = DateTime.UtcNow };

                Console.WriteLine($"\n  📝 Setting: {testKey}");
                await kvCache.SetAsync(testKey, testValue, TimeSpan.FromMinutes(5));
                Console.WriteLine($"  ✅ Set successful");

                // Test Get
                Console.WriteLine($"  🔍 Getting: {testKey}");
                var retrieved = await kvCache.GetAsync<dynamic>(testKey);
                Console.WriteLine($"  ✅ Retrieved: {(retrieved != null ? "Success" : "Failed")}");
                if (retrieved != null)
                {
                    Console.WriteLine($"     └─ Value: {retrieved}");
                }

                // Test Exists
                Console.WriteLine($"  🔍 Checking existence: {testKey}");
                var exists = await kvCache.ExistsAsync(testKey);
                Console.WriteLine($"  ✅ Exists: {exists}");

                // Test Remove
                Console.WriteLine($"  🗑️ Removing: {testKey}");
                await kvCache.RemoveAsync(testKey);
                Console.WriteLine($"  ✅ Removed");

                // Verify removal
                var afterRemove = await kvCache.GetAsync<dynamic>(testKey);
                Console.WriteLine($"  ✅ After Remove: {(afterRemove == null ? "Not found (correct)" : "Found (incorrect)")}");

                Console.WriteLine($"\n  📊 Final Count: {kvCache.Count}");
                Console.WriteLine("  ✅ KV Cache Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Demo 2: Token Cache

        public async Task DemoTokenCacheAsync()
        {
            Console.WriteLine("\n🔐 DEMO 2: Token Cache");
            Console.WriteLine(new string('─', 60));

            try
            {
                var tokenCache = _serviceProvider.GetService<ITokenCache>();
                if (tokenCache == null)
                {
                    Console.WriteLine("  ❌ ITokenCache not registered");
                    return;
                }

                Console.WriteLine($"  📋 Cache Type: {tokenCache.GetType().Name}");

                // Test Set and Get
                var testKey = "demo:token:test:1";
                var testValue = "This is a test token cached value";

                Console.WriteLine($"\n  📝 Setting: {testKey}");
                tokenCache.Set(testKey, testValue, tokenCount: 25);
                Console.WriteLine($"  ✅ Set successful");

                Console.WriteLine($"  🔍 Getting: {testKey}");
                var retrieved = tokenCache.TryGet<string>(testKey, out var value);
                Console.WriteLine($"  ✅ Retrieved: {retrieved}");
                if (retrieved)
                {
                    Console.WriteLine($"     └─ Value: {value}");
                }

                // Test GetOrAdd
                var key2 = "demo:token:test:2";
                Console.WriteLine($"\n  🔄 GetOrAdd: {key2}");
                var result = await tokenCache.GetOrAddAsync<string>(
                    key2,
                    async () =>
                    {
                        Console.WriteLine($"     └─ Factory called (generating value)");
                        await Task.Delay(100);
                        return "Generated value for demo";
                    },
                    tokenCount: 30,
                    costPerToken: 0.000001m
                );
                Console.WriteLine($"  ✅ Result: {result}");

                // Second call should be cached
                Console.WriteLine($"  🔄 GetOrAdd (cached): {key2}");
                var cached = await tokenCache.GetOrAddAsync<string>(
                    key2,
                    async () =>
                    {
                        Console.WriteLine($"     └─ Factory called again (should NOT happen)");
                        await Task.Delay(100);
                        return "This should not be used";
                    },
                    tokenCount: 30,
                    costPerToken: 0.000001m
                );
                Console.WriteLine($"  ✅ Cached Result: {cached}");

                // Get stats
                var stats = tokenCache.GetStats();
                Console.WriteLine($"\n  📊 Token Cache Stats:");
                Console.WriteLine($"     Cache Hits: {stats.CacheHits}");
                Console.WriteLine($"     Cache Misses: {stats.CacheMisses}");
                Console.WriteLine($"     Hit Rate: {stats.CacheHitRate:P2}");
                Console.WriteLine($"     Tokens Cached: {stats.TotalTokensCached}");
                Console.WriteLine($"     Tokens Saved: {stats.TotalTokensSaved}");
                Console.WriteLine($"     Cost Saved: ${stats.TotalCostSaved:F4}");

                Console.WriteLine("  ✅ Token Cache Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Demo 3: AI Response with Caching

        public async Task DemoAIResponseWithCachingAsync()
        {
            Console.WriteLine("\n🤖 DEMO 3: AI Response with Caching");
            Console.WriteLine(new string('─', 60));

            try
            {
                var aiService = _serviceProvider.GetService<IAIResponseService>();
                if (aiService == null)
                {
                    Console.WriteLine("  ❌ IAIResponseService not registered");
                    return;
                }

                Console.WriteLine($"  📋 Service Type: {aiService.GetType().Name}");

                var queries = new[]
                {
                    "What is artificial intelligence?",
                    "Explain machine learning in simple terms.",
                    "What is the difference between AI and ML?",
                    "Tell me about neural networks.",
                    "What is natural language processing?"
                };

                foreach (var query in queries.Take(3)) // Demo with 3 queries
                {
                    Console.WriteLine($"\n  📝 Query: {query}");

                    // First call - Cache MISS
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

                    Console.WriteLine($"      ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"      💾 From Cache: {(response1.FromCache ? "Yes" : "No")}");
                    Console.WriteLine($"      📊 Tokens: {response1.TokenCount}");
                    Console.WriteLine($"      🏷️ Cache Level: {response1.CacheLevel}");
                    Console.WriteLine($"      📝 Response Preview: {(response1.Response?.Length > 80 ? response1.Response.Substring(0, 80) + "..." : response1.Response)}");

                    // Second call - Should be Cache HIT
                    Console.WriteLine("    🔄 Second call (should be cache HIT)...");
                    stopwatch.Restart();

                    var response2 = await aiService.GenerateResponseAsync(request);
                    stopwatch.Stop();

                    Console.WriteLine($"      ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"      💾 From Cache: {(response2.FromCache ? "Yes" : "No")}");
                    Console.WriteLine($"      📊 Tokens: {response2.TokenCount}");
                    Console.WriteLine($"      🏷️ Cache Level: {response2.CacheLevel}");

                    // Verify caching worked
                    if (response1.FromCache == false && response2.FromCache == true)
                    {
                        Console.WriteLine($"      ✅ Caching WORKED! ({(stopwatch.ElapsedMilliseconds < 50 ? "Fast response" : "Cache hit")})");
                    }
                    else
                    {
                        Console.WriteLine($"      ⚠️ Caching may not be working properly");
                    }

                    // Show similarity
                    var similar = response1.Response == response2.Response;
                    Console.WriteLine($"      📋 Response Identical: {(similar ? "Yes ✅" : "No ⚠️")}");
                }

                Console.WriteLine("  ✅ AI Response Demo Completed!");
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

        #region Demo 4: Search Service

        public async Task DemoSearchAsync()
        {
            Console.WriteLine("\n🔍 DEMO 4: Search Service");
            Console.WriteLine(new string('─', 60));

            try
            {
                var searchService = _serviceProvider.GetService<ISearchService>();
                if (searchService == null)
                {
                    Console.WriteLine("  ❌ ISearchService not registered");
                    return;
                }

                Console.WriteLine($"  📋 Service Type: {searchService.GetType().Name}");

                var queries = new[]
                {
                    "artificial intelligence",
                    "machine learning",
                    "neural networks"
                };

                foreach (var query in queries)
                {
                    Console.WriteLine($"\n  📝 Query: {query}");

                    var stopwatch = Stopwatch.StartNew();
                    var response = await searchService.SearchAsync(query);
                    stopwatch.Stop();

                    Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"    📊 Results: {response.TotalCount}");
                    Console.WriteLine($"    🔍 Search Method: {response.SearchMethod}");

                    if (response.Results.Any())
                    {
                        Console.WriteLine($"    📄 Top Result:");
                        var top = response.Results.First();
                        Console.WriteLine($"       Title: {top.Title ?? "N/A"}");
                        Console.WriteLine($"       Score: {top.Score:F2}");
                        Console.WriteLine($"       Source: {top.Source ?? "N/A"}");
                    }
                }

                // Test Lexical Search
                Console.WriteLine($"\n  📝 Lexical Search: 'deep learning'");
                var lexicalResponse = await searchService.LexicalSearchAsync("deep learning", 5);
                Console.WriteLine($"    📊 Results: {lexicalResponse.TotalCount}");

                // Test Semantic Search
                Console.WriteLine($"\n  📝 Semantic Search: 'deep learning'");
                var semanticResponse = await searchService.SemanticSearchAsync("deep learning", 5);
                Console.WriteLine($"    📊 Results: {semanticResponse.TotalCount}");

                Console.WriteLine("  ✅ Search Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Demo 5: FAQ Service

        public async Task DemoFAQServiceAsync()
        {
            Console.WriteLine("\n❓ DEMO 5: FAQ Service");
            Console.WriteLine(new string('─', 60));

            try
            {
                var faqService = _serviceProvider.GetService<IFaqService>();
                if (faqService == null)
                {
                    Console.WriteLine("  ❌ IFaqService not registered");
                    return;
                }

                Console.WriteLine($"  📋 Service Type: {faqService.GetType().Name}");

                var questions = new[]
                {
                    "What is AI?",
                    "How does machine learning work?",
                    "What is deep learning?",
                    "What is natural language processing?"
                };

                foreach (var question in questions.Take(2))
                {
                    Console.WriteLine($"\n  📝 Question: {question}");

                    var stopwatch = Stopwatch.StartNew();
                    var response = await faqService.GetAllFaqsAsync();
                    stopwatch.Stop();

                    Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                    //Console.WriteLine($"    💾 From Cache: {(response. ? "Yes" : "No")}");
                    //Console.WriteLine($"    📊 Confidence: {response.Confidence:F2}");

                    //if (!string.IsNullOrEmpty(response.Answer))
                    //{
                    //    Console.WriteLine($"    📝 Answer Preview: {(response.Answer.Length > 80 ? response.Answer.Substring(0, 80) + "..." : response.Answer)}");
                    //}
                }

                Console.WriteLine("  ✅ FAQ Service Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Demo 6: Token Optimized Service

        public async Task DemoTokenOptimizedServiceAsync()
        {
            Console.WriteLine("\n💰 DEMO 6: Token Optimized Service");
            Console.WriteLine(new string('─', 60));

            try
            {
                var tokenService = _serviceProvider.GetService<ITokenOptimizedService>();
                if (tokenService == null)
                {
                    Console.WriteLine("  ❌ ITokenOptimizedService not registered");
                    return;
                }

                Console.WriteLine($"  📋 Service Type: {tokenService.GetType().Name}");

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
                        Metadata = new Dictionary<string, object>
                        {
                            ["Category"] = "General Knowledge",
                            ["Priority"] = "High"
                        }
                    };

                    Console.WriteLine($"\n  📝 Query: {query}");

                    // First call
                    var stopwatch = Stopwatch.StartNew();
                    var response1 = await tokenService.GetOptimizedResponseAsync(query, options);
                    stopwatch.Stop();

                    Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"    💾 From Cache: {(response1.FromCache ? "Yes" : "No")}");
                    Console.WriteLine($"    📊 Confidence: {response1.Confidence:F2}");
                    if (!string.IsNullOrEmpty(response1.Answer))
                    {
                        Console.WriteLine($"    📝 Answer Preview: {(response1.Answer.Length > 80 ? response1.Answer.Substring(0, 80) + "..." : response1.Answer)}");
                    }

                    // Second call - should be cached
                    Console.WriteLine($"    🔄 Second call (cached)...");
                    stopwatch.Restart();
                    var response2 = await tokenService.GetOptimizedResponseAsync(query, options);
                    stopwatch.Stop();

                    Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"    💾 From Cache: {(response2.FromCache ? "Yes" : "No")}");

                    if (response1.FromCache == false && response2.FromCache == true)
                    {
                        Console.WriteLine($"    ✅ Caching WORKED!");
                    }
                }

                // Batch test
                Console.WriteLine($"\n  📊 Batch Processing Test:");
                var batchQueries = new List<string> { "What is AI?", "What is ML?", "What is DL?" };
                var batchResults = await tokenService.GetBatchOptimizedResponsesAsync(batchQueries);
                Console.WriteLine($"    ✅ Processed {batchResults.Count} queries");

                foreach (var kvp in batchResults)
                {
                    Console.WriteLine($"       {kvp.Key}: {(kvp.Value.FromCache ? "✅" : "❌")} cached, {kvp.Value.Confidence:F2} confidence");
                }

                Console.WriteLine("  ✅ Token Optimized Service Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Demo 7: Cache Statistics

        public async Task DemoCacheStatisticsAsync()
        {
            Console.WriteLine("\n📊 DEMO 7: Cache Statistics");
            Console.WriteLine(new string('─', 60));

            try
            {
                var tokenCache = _serviceProvider.GetService<ITokenCache>();
                if (tokenCache == null)
                {
                    Console.WriteLine("  ❌ ITokenCache not registered");
                    return;
                }

                var kvCache = _serviceProvider.GetService<IKVCache>();
                if (kvCache == null)
                {
                    Console.WriteLine("  ❌ IKVCache not registered");
                    return;
                }

                Console.WriteLine("\n  📊 Token Cache Statistics:");
                var tokenStats = tokenCache.GetStats();
                Console.WriteLine($"     Cache Hits: {tokenStats.CacheHits:N0}");
                Console.WriteLine($"     Cache Misses: {tokenStats.CacheMisses:N0}");
                Console.WriteLine($"     Hit Rate: {tokenStats.CacheHitRate:P2}");
                Console.WriteLine($"     Tokens Cached: {tokenStats.TotalTokensCached:N0}");
                Console.WriteLine($"     Tokens Saved: {tokenStats.TotalTokensSaved:N0}");
                Console.WriteLine($"     Cost Saved: ${tokenStats.TotalCostSaved:F4}");
                Console.WriteLine($"     Cache Entries: {tokenStats.CacheEntryCount:N0}");

                Console.WriteLine("\n  📊 KV Cache Statistics:");
                var kvStats = kvCache.GetStatistics();
                Console.WriteLine($"     Total Items: {kvStats.TotalItems:N0}");
                Console.WriteLine($"     Cache Name: {kvStats.CacheName}");
                Console.WriteLine($"     Last Updated: {kvStats.LastUpdated:yyyy-MM-dd HH:mm:ss}");

                // Get detailed stats if available
                if (kvCache is MultiTierKVCache multiTier)
                {
                    Console.WriteLine("\n  📊 Multi-Tier Cache Statistics:");
                    var tierStats = multiTier.GetAllTierStatistics();
                    foreach (var tier in tierStats)
                    {
                        Console.WriteLine($"     {tier.Key}: {tier.Value.TotalItems:N0} items, Hits: {tier.Value.Hits:N0}, Misses: {tier.Value.Misses:N0}");
                    }
                }

                // Generate report
                Console.WriteLine("\n  📄 Token Cache Report:");
                var report = await tokenCache.GenerateReportAsync();
                Console.WriteLine(report);

                Console.WriteLine("  ✅ Cache Statistics Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Demo 8: Clear Cache

        public async Task DemoClearCacheAsync()
        {
            Console.WriteLine("\n🗑️ DEMO 8: Clear Cache");
            Console.WriteLine(new string('─', 60));

            try
            {
                var tokenCache = _serviceProvider.GetService<ITokenCache>();
                if (tokenCache == null)
                {
                    Console.WriteLine("  ❌ ITokenCache not registered");
                    return;
                }

                var kvCache = _serviceProvider.GetService<IKVCache>();
                if (kvCache == null)
                {
                    Console.WriteLine("  ❌ IKVCache not registered");
                    return;
                }

                // Show before stats
                Console.WriteLine("\n  📊 Before Clear:");
                var beforeStats = tokenCache.GetStats();
                Console.WriteLine($"     Tokens Cached: {beforeStats.TotalTokensCached:N0}");
                Console.WriteLine($"     Tokens Saved: {beforeStats.TotalTokensSaved:N0}");
                Console.WriteLine($"     Cache Entries: {beforeStats.CacheEntryCount:N0}");
                Console.WriteLine($"     KV Items: {kvCache.Count:N0}");

                // Clear token cache
                Console.WriteLine("\n  🗑️ Clearing Token Cache...");
                tokenCache.Clear();
                Console.WriteLine("  ✅ Token Cache Cleared!");

                // Clear KV cache if requested
                if (kvCache != null)
                {
                    Console.WriteLine("  🗑️ Clearing KV Cache...");
                    await kvCache.ClearAsync();
                    Console.WriteLine("  ✅ KV Cache Cleared!");
                }

                // Show after stats
                Console.WriteLine("\n  📊 After Clear:");
                var afterStats = tokenCache.GetStats();
                Console.WriteLine($"     Tokens Cached: {afterStats.TotalTokensCached:N0}");
                Console.WriteLine($"     Tokens Saved: {afterStats.TotalTokensSaved:N0}");
                Console.WriteLine($"     Cache Entries: {afterStats.CacheEntryCount:N0}");
                if (kvCache != null)
                {
                    Console.WriteLine($"     KV Items: {kvCache.Count:N0}");
                }

                Console.WriteLine("  ✅ Clear Cache Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion
    }
}