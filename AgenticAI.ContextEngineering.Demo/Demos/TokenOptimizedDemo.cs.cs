// AgenticAI.ContextEngineering.Demo/Demos/TokenOptimizedDemo.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class TokenOptimizedDemo : IDemo
    {
        private readonly ITokenOptimizedService _tokenService;

        public string Name => "Token Optimized Demo";
        public string Description => "Tests Token Optimized Service with caching and batch processing";
        public bool IsConfigured => _tokenService != null;
        public string ConfigurationStatus => _tokenService != null ? "✅ Configured" : "❌ Not Configured";

        public TokenOptimizedDemo(ITokenOptimizedService tokenService)
        {
            _tokenService = tokenService;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("\n💰 TOKEN OPTIMIZED DEMO");
            Console.WriteLine(new string('─', 60));

            if (!IsConfigured)
            {
                Console.WriteLine("  ❌ ITokenOptimizedService not registered");
                return;
            }

            try
            {
                Console.WriteLine($"  📋 Service Type: {_tokenService.GetType().Name}");

                // Test 1: Single Query
                await TestSingleQueryAsync();

                // Test 2: Caching
                await TestCachingAsync();

                // Test 3: Batch Processing
                await TestBatchProcessingAsync();

                // Test 4: Different Options
                await TestDifferentOptionsAsync();

                // Test 5: Stats
                await TestStatsAsync();

                Console.WriteLine("  ✅ Token Optimized Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                Console.WriteLine($"     Stack: {ex.StackTrace}");
            }
        }

        private async Task TestSingleQueryAsync()
        {
            Console.WriteLine("\n  📝 Test 1: Single Query");

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

                Console.WriteLine($"\n    Query: {query}");
                var stopwatch = Stopwatch.StartNew();
                var response = await _tokenService.GetOptimizedResponseAsync(query, options);
                stopwatch.Stop();

                Console.WriteLine($"      ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"      💾 From Cache: {(response.FromCache ? "Yes" : "No")}");
                Console.WriteLine($"      📊 Confidence: {response.Confidence:F2}");

                if (!string.IsNullOrEmpty(response.Answer))
                {
                    Console.WriteLine($"      📝 Answer: {(response.Answer.Length > 80 ? response.Answer.Substring(0, 80) + "..." : response.Answer)}");
                }

                if (response.TokenUsage != null)
                {
                    Console.WriteLine($"      📊 Tokens: {response.TokenUsage.TotalTokens} (Prompt: {response.TokenUsage.PromptTokens}, Completion: {response.TokenUsage.CompletionTokens})");
                }
            }
        }

        private async Task TestCachingAsync()
        {
            Console.WriteLine("\n  💾 Test 2: Caching");
            var query = "What is artificial intelligence?";

            Console.WriteLine($"    Query: {query}");

            // First call
            Console.WriteLine("    🔄 First call...");
            var stopwatch = Stopwatch.StartNew();
            var response1 = await _tokenService.GetOptimizedResponseAsync(query);
            stopwatch.Stop();
            Console.WriteLine($"      ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"      💾 From Cache: {response1.FromCache}");

            // Second call
            Console.WriteLine("    🔄 Second call...");
            stopwatch.Restart();
            var response2 = await _tokenService.GetOptimizedResponseAsync(query);
            stopwatch.Stop();
            Console.WriteLine($"      ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"      💾 From Cache: {response2.FromCache}");

            // Verify
            if (response1.FromCache == false && response2.FromCache == true)
            {
                Console.WriteLine($"      ✅ Caching WORKED!");
            }
            else
            {
                Console.WriteLine($"      ⚠️ Caching issue detected");
            }
        }

        private async Task TestBatchProcessingAsync()
        {
            Console.WriteLine("\n  📊 Test 3: Batch Processing");

            var queries = new List<string>
            {
                "What is AI?",
                "What is Machine Learning?",
                "What is Deep Learning?",
                "What is Natural Language Processing?",
                "What is Computer Vision?"
            };

            Console.WriteLine($"    Processing {queries.Count} queries...");
            var stopwatch = Stopwatch.StartNew();
            var results = await _tokenService.GetBatchOptimizedResponsesAsync(queries);
            stopwatch.Stop();

            Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"    📊 Results: {results.Count}");

            foreach (var kvp in results)
            {
                var status = kvp.Value.FromCache ? "✅" : "❌";
                var confidence = kvp.Value.Confidence;
                var preview = kvp.Value.Answer?.Length > 50 ? kvp.Value.Answer.Substring(0, 50) + "..." : kvp.Value.Answer;
                Console.WriteLine($"      {status} {kvp.Key} (Confidence: {confidence:F2})");
                Console.WriteLine($"         └─ {preview}");
            }
        }

        private async Task TestDifferentOptionsAsync()
        {
            Console.WriteLine("\n  ⚙️ Test 4: Different Options");

            var query = "Explain neural networks";

            var optionSets = new[]
            {
                new { Name = "Brief", MaxTokens = 100, Temperature = 0.3f },
                new { Name = "Detailed", MaxTokens = 300, Temperature = 0.7f },
                new { Name = "Creative", MaxTokens = 150, Temperature = 0.9f }
            };

            foreach (var opt in optionSets)
            {
                Console.WriteLine($"\n    Options: {opt.Name} (MaxTokens: {opt.MaxTokens}, Temp: {opt.Temperature})");

                var options = new OptimizedRequestOptions
                {
                    SystemPrompt = "You are a helpful assistant.",
                    MaxTokens = opt.MaxTokens,
                    Temperature = opt.Temperature,
                    UseCache = false // Don't cache for this test
                };

                var stopwatch = Stopwatch.StartNew();
                var response = await _tokenService.GetOptimizedResponseAsync(query, options);
                stopwatch.Stop();

                Console.WriteLine($"      ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"      📊 Tokens: {response.TokenUsage?.TotalTokens ?? 0}");
                Console.WriteLine($"      📝 Answer: {(response.Answer?.Length > 60 ? response.Answer.Substring(0, 60) + "..." : response.Answer)}");
            }
        }

        private async Task TestStatsAsync()
        {
            Console.WriteLine("\n  📊 Test 5: Statistics");

            var stats = _tokenService.GetTokenStats();
            Console.WriteLine($"    Total Tokens Cached: {stats.TotalTokensCached:N0}");
            Console.WriteLine($"    Total Tokens Saved: {stats.TotalTokensSaved:N0}");
            Console.WriteLine($"    Cache Hit Rate: {stats.CacheHitRate:P2}");
            Console.WriteLine($"    Cost Saved: ${stats.TotalCostSaved:F4}");
            Console.WriteLine($"    Cache Entries: {stats.CacheEntryCount:N0}");
        }
    }
}