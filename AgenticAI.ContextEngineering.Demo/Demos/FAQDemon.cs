// AgenticAI.ContextEngineering.Demo/Demos/FAQDemon.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class FAQDemo : IDemo
    {
        private readonly IFaqService _faqService;

        public string Name => "FAQ Demo";
        public string Description => "Tests FAQ Service (Search, Answer, Statistics)";
        public bool IsConfigured => _faqService != null;
        public string ConfigurationStatus => _faqService != null ? "✅ Configured" : "❌ Not Configured";

        public FAQDemo(IFaqService faqService)
        {
            _faqService = faqService;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("\n❓ FAQ DEMO");
            Console.WriteLine(new string('─', 60));

            if (!IsConfigured)
            {
                Console.WriteLine("  ❌ IFaqService not registered");
                return;
            }

            try
            {
                Console.WriteLine($"  📋 Service Type: {_faqService.GetType().Name}");

                // Test 1: Single Answer
                await TestSingleAnswerAsync();

                // Test 2: Search
                await TestSearchAsync();

                // Test 3: Get by ID
                await TestGetByIdAsync();

                // Test 4: Statistics
                await TestStatisticsAsync();

                // Test 5: All FAQs
                await TestGetAllAsync();

                Console.WriteLine("  ✅ FAQ Demo Completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                Console.WriteLine($"     Stack: {ex.StackTrace}");
            }
        }

        private async Task TestSingleAnswerAsync()
        {
            Console.WriteLine("\n  📝 Test 1: Single Question Answering");

            var questions = new[]
            {
        "What is artificial intelligence?",
        "What is machine learning?",
        "What is the difference between AI and ML?",
        "What is the meaning of life?"
    };

            foreach (var question in questions)
            {
                Console.WriteLine($"\n    Q: {question}");

                var stopwatch = Stopwatch.StartNew();

                var results = await _faqService.SearchFaqsAsync(
                    question,
                    topResults: 1,
                    minScore: 0.1);

                stopwatch.Stop();

                var result = results?.FirstOrDefault();

                if (result == null)
                {
                    Console.WriteLine("    A: No answer found");
                    Console.WriteLine(
                        $"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine("    📊 Score: 0.00");
                    Console.WriteLine("    🏷️ Category: Uncategorized");
                    continue;
                }

                var answer = result.Content ?? "No answer found";

                Console.WriteLine(
                    $"    A: {(answer.Length > 100
                        ? answer.Substring(0, 100) + "..."
                        : answer)}");

                Console.WriteLine(
                    $"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");

                Console.WriteLine(
                    $"    📊 Score: {result.Score:F2}");

                //Console.WriteLine(
                //    $"    🏷️ Category: {result.Metadata ?? "Uncategorized"}");

                //if (result.MatchedKeywords != null &&
                //    result.MatchedKeywords.Count > 0)
                //{
                //    Console.WriteLine(
                //        $"    🔑 Keywords: {string.Join(", ", result.MatchedKeywords)}");
                //}
            }
        }

        private async Task TestSearchAsync()
        {
            Console.WriteLine("\n  🔍 Test 2: FAQ Search");

            var requests = new[]
            {
                new FaqSearchRequest { Query = "neural network", TopResults = 3 },
                new FaqSearchRequest { Query = "cloud computing", TopResults = 3 },
                new FaqSearchRequest { Query = "DevOps", TopResults = 3 }
            };

            foreach (var request in requests)
            {
                Console.WriteLine($"\n    Query: {request.Query}");
                var stopwatch = Stopwatch.StartNew();
                var response = await _faqService.SearchAsync(request);
                stopwatch.Stop();

                Console.WriteLine($"    ⏱️ Time: {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"    📊 Results: {response.TotalResults}");
                Console.WriteLine($"    💾 From Cache: {(response.FromCache ? "Yes" : "No")}");

                if (response.Results != null && response.Results.Count > 0)
                {
                    foreach (var result in response.Results)
                    {
                        Console.WriteLine($"      └─ [{result.RelevanceScore:F2}] {result.Question}");
                    }
                }
                else
                {
                    Console.WriteLine($"      ⚠️ No results found");
                }
            }
        }

        private async Task TestGetByIdAsync()
        {
            Console.WriteLine("\n  📄 Test 3: Get FAQ by ID");

            var ids = new[] { "faq-001", "faq-005", "faq-999" };

            foreach (var id in ids)
            {
                Console.WriteLine($"\n    ID: {id}");
                var result = await _faqService.GetFaqByIdAsync(id);

                if (result != null)
                {
                    Console.WriteLine($"      ✅ Found: {result.Question}");
                    Console.WriteLine($"         Category: {result.Category}");
                    Console.WriteLine($"         Answer: {(result.Answer?.Length > 80 ? result.Answer.Substring(0, 80) + "..." : result.Answer)}");
                }
                else
                {
                    Console.WriteLine($"      ❌ Not found");
                }
            }
        }

        private async Task TestStatisticsAsync()
        {
            Console.WriteLine("\n  📊 Test 4: FAQ Statistics");

            var stats = await _faqService.GetStatisticsAsync();

            Console.WriteLine($"    Total FAQs: {stats.TotalFaqs}");
            Console.WriteLine($"    Last Updated: {stats.LastUpdated:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"    Is Indexed: {stats.IsIndexed}");
            Console.WriteLine($"    Search Index Count: {stats.SearchIndexCount}");

            Console.WriteLine($"\n    📂 Categories:");
            foreach (var cat in stats.CategoryCounts)
            {
                Console.WriteLine($"      └─ {cat.Key}: {cat.Value} FAQs");
            }

            Console.WriteLine($"\n    🌐 Languages:");
            foreach (var lang in stats.LanguageCounts)
            {
                var langName = lang.Key == 1 ? "English" : $"Language {lang.Key}";
                Console.WriteLine($"      └─ {langName}: {lang.Value} FAQs");
            }
        }

        private async Task TestGetAllAsync()
        {
            Console.WriteLine("\n  📚 Test 5: All FAQs");

            var faqs = await _faqService.GetAllFaqsAsync();
            Console.WriteLine($"    Total: {faqs?.Count ?? 0} FAQs");

            if (faqs != null && faqs.Count > 0)
            {
                var sample = 3;
                Console.WriteLine($"\n    Showing {Math.Min(sample, faqs.Count)} sample FAQs:");
                for (int i = 0; i < Math.Min(sample, faqs.Count); i++)
                {
                    var faq = faqs[i];
                    Console.WriteLine($"      {i + 1}. [{faq.Id}] {faq.Question}");
                    Console.WriteLine($"         Category: {faq.Category}");
                }
            }
        }
    }
}