// AgenticAI.ContextEngineering.Demo/Demos/AIResponseDemo.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class AIResponseDemo : IDemo
    {
        private readonly IAIResponseService _aiService;
        private readonly ITokenCache _tokenCache;

        public string Name => "AI Response Demo";
        public string Description => "Tests AI Response Service with caching (KV Cache + Token Cache)";
        public bool IsConfigured => _aiService != null;
        public string ConfigurationStatus => _aiService != null ? "✅ Configured" : "❌ Not Configured";

        public AIResponseDemo(IAIResponseService aiService, ITokenCache tokenCache)
        {
            _aiService = aiService;
            _tokenCache = tokenCache;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("\n🤖 AI RESPONSE DEMO");
            Console.WriteLine(new string('─', 60));

            if (!IsConfigured)
            {
                Console.WriteLine("  ❌ IAIResponseService not registered");
                return;
            }

            try
            {
                Console.WriteLine($"  📋 Service Type: {_aiService.GetType().Name}");

                var queries = new[]
                {
                    "What is artificial intelligence?",
                    "Explain machine learning in simple terms.",
                    "What is the difference between AI and ML?"
                };

                var results = new List<AIResponseResult>();

                foreach (var query in queries)
                {
                    var (response1, response2) = await TestQueryAsync(query);
                    results.Add(response1);
                    results.Add(response2);
                }

                // Show summary
                await ShowSummaryAsync(results);

                Console.WriteLine("  ✅ AI Response Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                Console.WriteLine($"     Stack: {ex.StackTrace}");
            }
        }

        private async Task<(AIResponseResult First, AIResponseResult Second)> TestQueryAsync(string query)
        {
            Console.WriteLine($"\n  📝 Query: {query}");

            var request = new AIResponseRequest
            {
                Query = query,
                UserQuery = query,
                Temperature = 0.7f,
                MaxTokens = 200,
                UseCache = true,
                SystemPrompt = "You are a helpful assistant. Provide concise answers."
            };

            // First call - Cache MISS
            Console.WriteLine("    🔄 First call (should be cache MISS)...");
            var stopwatch = Stopwatch.StartNew();
            var response1 = await _aiService.GenerateResponseAsync(request);
            stopwatch.Stop();

            LogResponse(response1, stopwatch);

            // Second call - Cache HIT
            Console.WriteLine("    🔄 Second call (should be cache HIT)...");
            stopwatch.Restart();
            var response2 = await _aiService.GenerateResponseAsync(request);
            stopwatch.Stop();

            LogResponse(response2, stopwatch);

            // Verify caching
            VerifyCaching(response1, response2);

            return (response1, response2);
        }

        private void LogResponse(AIResponseResult response, Stopwatch stopwatch)
        {
            Console.WriteLine($"      ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"      💾 From Cache: {(response.FromCache ? "Yes" : "No")}");
            Console.WriteLine($"      📊 Tokens: {response.TokenCount}");
            Console.WriteLine($"      🏷️ Cache Level: {response.CacheLevel}");
            Console.WriteLine($"      📝 Response Preview: {(response.Response?.Length > 80 ? response.Response.Substring(0, 80) + "..." : response.Response)}");
        }

        private void VerifyCaching(AIResponseResult first, AIResponseResult second)
        {
            if (first.FromCache == false && second.FromCache == true)
            {
                Console.WriteLine($"      ✅ Caching WORKED!");
            }
            else if (first.FromCache == false && second.FromCache == false)
            {
                Console.WriteLine($"      ⚠️ Both calls were cache MISS");
            }
            else if (first.FromCache == true && second.FromCache == true)
            {
                Console.WriteLine($"      ⚠️ Both calls were cache HIT (unexpected for first call)");
            }
        }

        private async Task ShowSummaryAsync(List<AIResponseResult> results)
        {
            Console.WriteLine("\n  📊 Summary:");
            var cached = results.Count(r => r.FromCache);
            var total = results.Count;
            Console.WriteLine($"    Total Calls: {total}");
            Console.WriteLine($"    From Cache: {cached}");
            Console.WriteLine($"    Hit Rate: {(total > 0 ? (double)cached / total : 0):P2}");

            if (_tokenCache != null)
            {
                var stats = _tokenCache.GetStats();
                Console.WriteLine($"    Token Cache Hits: {stats.CacheHits}");
                Console.WriteLine($"    Token Cache Misses: {stats.CacheMisses}");
                Console.WriteLine($"    Total Tokens Saved: {stats.TotalTokensSaved}");
            }
        }
    }
}