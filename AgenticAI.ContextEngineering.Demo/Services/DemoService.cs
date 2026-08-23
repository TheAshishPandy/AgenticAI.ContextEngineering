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

namespace AgenticAI.ContextEngineering.Demo.Service
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

        #region Test 1: Service Registration

        public async Task TestServiceRegistrationAsync()
        {
            _logger.LogInformation("📋 TEST 1: Service Registration Verification");

            var services = new Dictionary<Type, string>
            {
                { typeof(IAIResponseService), "AI Response Service" },
                { typeof(ISearchService), "Search Service" },
                { typeof(IFaqService), "FAQ Service" },
                { typeof(CachedSearchService), "Cached Search Service" },
                { typeof(ITokenOptimizedService), "Token Optimized Service" },
                { typeof(IKVCache), "KV Cache" },
                { typeof(ITokenCache), "Token Cache" }
            };

            var allRegistered = true;

            foreach (var kvp in services)
            {
                try
                {
                    var service = _serviceProvider.GetService(kvp.Key);
                    var isRegistered = service != null;
                    allRegistered = allRegistered && isRegistered;
                    Console.WriteLine($"  {(isRegistered ? "✅" : "❌")} {kvp.Value}: {(isRegistered ? "Registered" : "NOT Registered")}");
                }
                catch (Exception ex)
                {
                    allRegistered = false;
                    Console.WriteLine($"  ❌ {kvp.Value}: Error - {ex.Message}");
                }
            }

            Console.WriteLine($"\n  📊 Summary: {(allRegistered ? "✅ All services registered!" : "❌ Some services missing")}");
            Console.WriteLine(new string('=', 60));

            await Task.CompletedTask;
        }

        #endregion

        #region Test 2: AI Response Service

        public async Task TestAIResponseAsync()
        {
            Console.WriteLine("🤖 TEST 2: AI Response Service");

            try
            {
                var aiService = _serviceProvider.GetService<IAIResponseService>();
                if (aiService == null)
                {
                    Console.WriteLine("  ❌ IAIResponseService not registered");
                    return;
                }

                var request = new AIResponseRequest
                {
                    Query = "What is artificial intelligence?",
                    UserQuery = "What is artificial intelligence?",
                    Temperature = 0.7f,
                    MaxTokens = 200,
                    UseCache = true,
                    SystemPrompt = "You are a helpful assistant. Provide concise and accurate answers."
                };

                Console.WriteLine($"  📝 Query: {request.Query}");

                var stopwatch = Stopwatch.StartNew();
                var response = await aiService.GenerateResponseAsync(request);
                stopwatch.Stop();

                if (response != null && response.IsSuccess)
                {
                    var preview = response.Response?.Length > 100 ? response.Response.Substring(0, 100) + "..." : response.Response;
                    Console.WriteLine($"  ✅ Response: {preview}");
                    Console.WriteLine($"  ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"  📊 Tokens: {response.TokenCount} (Prompt: {response.PromptTokens}, Completion: {response.CompletionTokens})");
                    Console.WriteLine($"  💾 From Cache: {(response.FromCache ? "Yes" : "No")}");
                }
                else
                {
                    Console.WriteLine($"  ❌ Error: {response?.Error ?? "Unknown error"}");
                }

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Test 3: FAQ Service

        public async Task TestFaqServiceAsync()
        {
            Console.WriteLine("📚 TEST 3: FAQ Service");

            try
            {
                var faqService = _serviceProvider.GetService<IFaqService>();
                if (faqService == null)
                {
                    Console.WriteLine("  ❌ IFaqService not registered");
                    return;
                }

                // Try to load FAQ from default path
                var filePath = "Data/faq.json";
                await faqService.TryLoadAsync(filePath);

                if (!faqService.IsLoaded)
                {
                    Console.WriteLine("  ⚠️ FAQ file not loaded. Creating sample FAQ data...");
                    // Create sample FAQ data or use fallback
                }

                var queries = new[] { "return policy", "reset password", "payment" };

                foreach (var query in queries)
                {
                    Console.WriteLine($"  📝 Query: {query}");

                    var stopwatch = Stopwatch.StartNew();
                    var results = await faqService.SearchFaqsAsync(query, topResults: 2, minScore: 0.1);
                    stopwatch.Stop();

                    Console.WriteLine($"    ✅ Found: {results.Count} FAQs");
                    Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");

                    if (results.Count > 0)
                    {
                        var top = results[0];
                        Console.WriteLine($"    🏆 Top Result: {top.Title ?? "Untitled"}");
                        Console.WriteLine($"       Score: {top.Score:F3}");
                    }
                }

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Test 4: Token Optimized Service

        public async Task TestTokenOptimizedServiceAsync()
        {
            Console.WriteLine("💰 TEST 4: Token Optimized Service");

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

                    Console.WriteLine($"  📝 Query: {query}");

                    var stopwatch = Stopwatch.StartNew();
                    var response = await tokenService.GetOptimizedResponseAsync(query, options);
                    stopwatch.Stop();

                    if (response.IsSuccess)
                    {
                        var preview = response.Answer?.Length > 80 ? response.Answer.Substring(0, 80) + "..." : response.Answer;
                        Console.WriteLine($"    ✅ Answer: {preview}");
                        Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                        Console.WriteLine($"    💾 From Cache: {(response.FromCache ? "Yes" : "No")}");
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

        #region Test 5: RAG Pipeline

        public async Task TestRAGPipelineAsync()
        {
            Console.WriteLine("🧠 TEST 5: Full RAG Pipeline");

            try
            {
                var aiService = _serviceProvider.GetService<IAIResponseService>();
                var searchService = _serviceProvider.GetService<CachedSearchService>();

                if (aiService == null || searchService == null)
                {
                    Console.WriteLine("  ❌ Required services not registered");
                    return;
                }

                var query = "What is the future of AI technology?";
                Console.WriteLine($"  📝 Query: {query}");

                // Step 1: Search
                Console.WriteLine("  🔍 Step 1: Searching knowledge base...");
                var searchRequest = new SearchRequest
                {
                    Query = query,
                    TopResults = 3,
                    IncludeScoreBreakdown = true
                };

                var searchStopwatch = Stopwatch.StartNew();
                var searchResponse = await searchService.HybridSearchAsync(searchRequest);
                searchStopwatch.Stop();

                Console.WriteLine($"    ✅ Found {searchResponse.Results.Count} results in {searchStopwatch.ElapsedMilliseconds}ms");

                // Step 2: Generate AI Response with Context
                Console.WriteLine("  🤖 Step 2: Generating AI response with context...");

                var context = searchResponse.Results.Select(r => r.Content).ToList();
                var aiRequest = new AIResponseRequest
                {
                    Query = query,
                    UserQuery = query,
                    SystemPrompt = "You are a helpful assistant. Use the provided context to answer the question.",
                    Temperature = 0.5f,
                    MaxTokens = 300,
                    UseCache = true,
                    ModuleData = new Dictionary<string, string>
                    {
                        ["Context"] = string.Join("\n\n", context)
                    }
                };

                var aiStopwatch = Stopwatch.StartNew();
                var aiResponse = await aiService.GenerateResponseAsync(aiRequest);
                aiStopwatch.Stop();

                if (aiResponse.IsSuccess)
                {
                    var preview = aiResponse.Response?.Length > 150 ? aiResponse.Response.Substring(0, 150) + "..." : aiResponse.Response;
                    Console.WriteLine($"    ✅ Generated response in {aiStopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"    📝 Answer: {preview}");
                    Console.WriteLine($"    📊 Tokens: {aiResponse.TokenCount}");
                }
                else
                {
                    Console.WriteLine($"    ❌ Error: {aiResponse.Error}");
                }

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Test 6: Batch Processing

        public async Task TestBatchProcessingAsync()
        {
            Console.WriteLine("📦 TEST 6: Batch Processing");

            try
            {
                var tokenService = _serviceProvider.GetService<ITokenOptimizedService>();
                if (tokenService == null)
                {
                    Console.WriteLine("  ❌ ITokenOptimizedService not registered");
                    return;
                }

                var queries = new List<string>
                {
                    "What is AI?",
                    "What is machine learning?",
                    "What is deep learning?",
                    "What is natural language processing?",
                    "What is computer vision?"
                };

                Console.WriteLine($"  📝 Processing {queries.Count} queries in batch...");

                var options = new OptimizedRequestOptions
                {
                    SystemPrompt = "You are a helpful assistant. Give brief, accurate answers.",
                    MaxTokens = 100,
                    Temperature = 0.3f,
                    UseCache = true,
                    Metadata = new Dictionary<string, string>
                    {
                        ["BatchId"] = Guid.NewGuid().ToString(),
                        ["Category"] = "AI Concepts"
                    }
                };

                var stopwatch = Stopwatch.StartNew();
                var results = await tokenService.GetBatchOptimizedResponsesAsync(queries, options);
                stopwatch.Stop();

                Console.WriteLine($"  ✅ Completed in {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"  📊 Results: {results.Count} responses");

                foreach (var kvp in results)
                {
                    var isSuccess = kvp.Value.IsSuccess;
                    var key = kvp.Key.Length > 30 ? kvp.Key.Substring(0, 30) + "..." : kvp.Key;
                    var value = isSuccess && kvp.Value.Answer != null
                        ? kvp.Value.Answer.Length > 50 ? kvp.Value.Answer.Substring(0, 50) + "..." : kvp.Value.Answer
                        : kvp.Value.Error ?? "Unknown error";
                    Console.WriteLine($"    {(isSuccess ? "✅" : "❌")} {key} → {value}");
                }

                var stats = tokenService.GetTokenStats();
                Console.WriteLine($"\n  📊 Total Stats:");
                Console.WriteLine($"    Total Tokens Saved: {stats.TotalTokensSaved:N0}");
                Console.WriteLine($"    Cache Hit Rate: {stats.CacheHitRate:P2}");

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion

        #region Test 7: Streaming

        public async Task TestStreamingAsync()
        {
            Console.WriteLine("🌊 TEST 7: Streaming");

            try
            {
                var aiService = _serviceProvider.GetService<IAIResponseService>();
                if (aiService == null)
                {
                    Console.WriteLine("  ❌ IAIResponseService not registered");
                    return;
                }

                var query = "Explain the concept of machine learning in 3 sentences.";
                Console.WriteLine($"  📝 Query: {query}");
                Console.WriteLine("  📤 Streaming Response:");

                var request = new AIResponseRequest
                {
                    Query = query,
                    UserQuery = query,
                    Temperature = 0.5f,
                    MaxTokens = 150,
                    Stream = true,
                    SystemPrompt = "You are a helpful assistant. Give concise answers.",
                    ModuleData = new Dictionary<string, string>
                    {
                        ["Topic"] = "Machine Learning",
                        ["Format"] = "Concise"
                    }
                };

                var chunks = new List<string>();
                await foreach (var chunk in aiService.GenerateStreamingResponseAsync(request))
                {
                    if (!chunk.IsComplete && !string.IsNullOrEmpty(chunk.Content))
                    {
                        chunks.Add(chunk.Content);
                        Console.Write(chunk.Content);
                    }
                }

                Console.WriteLine("\n");
                Console.WriteLine($"  ✅ Complete Response: {string.Concat(chunks)}");
                Console.WriteLine($"  📊 Total Chunks: {chunks.Count}");

                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
            }
        }

        #endregion
        #region Test 8: Caching

        public async Task TestCachingAsync()
        {
            Console.WriteLine("🗄️ TEST 8: Caching Infrastructure");

            try
            {
                var cache = _serviceProvider.GetService<IKVCache>();
                if (cache == null)
                {
                    Console.WriteLine("  ❌ IKVCache not registered");
                    return;
                }

                var testKey = "test:key";
                var testValue = new { Name = "Test", Value = 123, Timestamp = DateTime.UtcNow };

                Console.WriteLine($"  📝 Setting cache: {testKey}");
                await cache.SetAsync(testKey, testValue, TimeSpan.FromMinutes(5));

                Console.WriteLine($"  🔍 Getting cache: {testKey}");

                // ✅ FIX: Use proper type instead of dynamic
                var retrieved = await cache.GetAsync<Dictionary<string, object>>(testKey);

                Console.WriteLine($"  ✅ Retrieved: {retrieved != null}");
                if (retrieved != null)
                {
                    // Safely access dictionary values
                    if (retrieved.TryGetValue("Name", out var nameObj))
                        Console.WriteLine($"     Name: {nameObj}");
                    if (retrieved.TryGetValue("Value", out var valueObj))
                        Console.WriteLine($"     Value: {valueObj}");
                    if (retrieved.TryGetValue("Timestamp", out var timeObj))
                        Console.WriteLine($"     Timestamp: {timeObj}");
                }

                Console.WriteLine($"  🗑️ Removing cache: {testKey}");
                await cache.RemoveAsync(testKey);

                var afterRemove = await cache.GetAsync<Dictionary<string, object>>(testKey);
                Console.WriteLine($"  ✅ After Remove: {(afterRemove == null ? "Not found (correct)" : "Found (incorrect)")}");

                var stats = cache.GetStatistics();
                Console.WriteLine($"\n  📊 Cache Statistics:");
                Console.WriteLine($"    Cache Name: {stats.CacheName}");
                Console.WriteLine($"    Total Items: {stats.TotalItems}");
                Console.WriteLine($"    Hit Rate: {stats.HitRate:P2}");

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