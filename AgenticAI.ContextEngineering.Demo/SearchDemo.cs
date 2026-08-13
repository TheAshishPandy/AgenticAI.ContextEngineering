// SmartChatBot.ContextEngineering.Demo/SearchDemo.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgenticAI.ContextEngineering.Core.Embeddings;
using AgenticAI.ContextEngineering.Core.Extensions;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartChatBot.ContextEngineering.Demo
{
    public class SearchDemo
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SearchDemo> _logger;

        public SearchDemo(IServiceProvider serviceProvider, ILogger<SearchDemo> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  🔍 SmartChatBot.Search - Complete Search Demo                              ║");
            Console.WriteLine("║  Lexical | Semantic | Hybrid Search with RRF                               ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            try
            {
                // Get services
                var searchIndex = _serviceProvider.GetRequiredService<SearchIndex>();
                var hybridSearch = _serviceProvider.GetRequiredService<HybridSearchEngine>();
                var lexicalSearch = _serviceProvider.GetRequiredService<LexicalSearch>();
                var semanticSearch = _serviceProvider.GetRequiredService<SemanticSearch>();
                var embeddingGen = _serviceProvider.GetRequiredService<IEmbeddingGenerator>();

                // ============================================================
                // STEP 1: Index Sample Documents
                // ============================================================
                Console.WriteLine("📝 Indexing Sample Documents...");
                Console.WriteLine(new string('─', 80));
                Console.WriteLine();

                var documents = CreateSampleDocuments();
                searchIndex.IndexDocuments(documents);
                Console.WriteLine($"✅ Indexed {documents.Count} documents");
                Console.WriteLine();

                // Display indexed documents
                Console.WriteLine("📚 Indexed Documents:");
                foreach (var doc in documents)
                {
                    Console.WriteLine($"   [{doc.Id}] {doc.Title}");
                    Console.WriteLine($"      {doc.Content[..Math.Min(60, doc.Content.Length)]}...");
                    Console.WriteLine($"      Source: {doc.Source}");
                    Console.WriteLine($"      Length: {doc.DocumentLength} chars");
                    Console.WriteLine();
                }

                // ============================================================
                // STEP 2: Lexical Search (BM25)
                // ============================================================
                await RunLexicalSearchDemoAsync(lexicalSearch);

                // ============================================================
                // STEP 3: Semantic Search (Vector)
                // ============================================================
                await RunSemanticSearchDemoAsync(semanticSearch, embeddingGen);

                // ============================================================
                // STEP 4: Hybrid Search (RRF)
                // ============================================================
                await RunHybridSearchDemoAsync(hybridSearch);

                // ============================================================
                // STEP 5: Search Comparison
                // ============================================================
                await RunSearchComparisonDemoAsync(hybridSearch, lexicalSearch, semanticSearch, embeddingGen);

                // ============================================================
                // STEP 6: Advanced Search Features
                // ============================================================
                await RunAdvancedSearchDemoAsync(hybridSearch);

                Console.WriteLine();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  ✅ Search Demo Completed Successfully!                                      ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
                Console.WriteLine();
                Console.WriteLine("📚 Key Takeaways:");
                Console.WriteLine("  1. Lexical Search: Best for exact keyword matching (BM25)");
                Console.WriteLine("  2. Semantic Search: Best for understanding user intent (Vectors)");
                Console.WriteLine("  3. Hybrid Search: Best of both worlds (RRF)");
                Console.WriteLine("  4. RRF combines rankings without needing to normalize scores");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        // ============================================================
        // CREATE SAMPLE DOCUMENTS
        // ============================================================

        private List<Document> CreateSampleDocuments()
        {
            return new List<Document>
            {
                new Document
                {
                    Id = "doc1",
                    Title = "Bank Account Balance",
                    Content = "Your bank account balance is $1,234.56. You have 5 transactions this month. " +
                              "The current balance includes pending deposits and withdrawals. " +
                              "Please check your statement for more details.",
                    Source = "Banking API",
                    Metadata = new Dictionary<string, object>
                    {
                        ["category"] = "banking",
                        ["priority"] = "high"
                    }
                },
                new Document
                {
                    Id = "doc2",
                    Title = "Recent Transactions",
                    Content = "Recent transactions: Jan 15: -$50.00 at Grocery Store, " +
                              "Jan 10: -$25.00 at Coffee Shop, " +
                              "Jan 5: +$100.00 from Salary Deposit, " +
                              "Dec 28: -$75.00 at Restaurant.",
                    Source = "Transaction Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["category"] = "transactions",
                        ["priority"] = "medium"
                    }
                },
                new Document
                {
                    Id = "doc3",
                    Title = "Pending Payments",
                    Content = "Pending payments: $75.00 due on Feb 1 for Utility Bill, " +
                              "$150.00 due on Feb 15 for Credit Card, " +
                              "$200.00 due on Mar 1 for Rent.",
                    Source = "Payment Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["category"] = "payments",
                        ["priority"] = "high"
                    }
                },
                new Document
                {
                    Id = "doc4",
                    Title = "Account Statement",
                    Content = "Monthly statement: Opening balance $1,200.00, " +
                              "Closing balance $1,234.56, " +
                              "Total deposits $500.00, " +
                              "Total withdrawals $465.44.",
                    Source = "Statement Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["category"] = "statement",
                        ["priority"] = "medium"
                    }
                },
                new Document
                {
                    Id = "doc5",
                    Title = "Credit Card Limit",
                    Content = "Credit card limit is $5,000.00. Current usage is $2,345.67. " +
                              "Available credit is $2,654.33. " +
                              "Your credit score is 780.",
                    Source = "Card Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["category"] = "credit",
                        ["priority"] = "high"
                    }
                },
                new Document
                {
                    Id = "doc6",
                    Title = "Loan Application Status",
                    Content = "Your loan application for $25,000 is currently under review. " +
                              "The estimated decision time is 2-3 business days. " +
                              "Please provide any additional documents if requested.",
                    Source = "Loan Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["category"] = "loans",
                        ["priority"] = "high"
                    }
                },
                new Document
                {
                    Id = "doc7",
                    Title = "Investment Portfolio",
                    Content = "Your investment portfolio summary: " +
                              "Stocks: $15,000 (60%), Bonds: $5,000 (20%), " +
                              "Cash: $3,000 (12%), Crypto: $2,000 (8%). " +
                              "Total portfolio value: $25,000.",
                    Source = "Investment Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["category"] = "investments",
                        ["priority"] = "medium"
                    }
                },
                new Document
                {
                    Id = "doc8",
                    Title = "Account Settings",
                    Content = "Account settings: 2FA is enabled, " +
                              "Email notifications are on, " +
                              "Monthly statements are sent via email, " +
                              "Mobile app is linked to your account.",
                    Source = "Settings Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["category"] = "settings",
                        ["priority"] = "low"
                    }
                }
            };
        }

        // ============================================================
        // LEXICAL SEARCH DEMO
        // ============================================================

        private async Task RunLexicalSearchDemoAsync(LexicalSearch lexicalSearch)
        {
            Console.WriteLine("🔍 1. LEXICAL SEARCH (BM25)");
            Console.WriteLine(new string('─', 80));
            Console.WriteLine();

            var queries = new[]
            {
                "account balance and transactions",
                "credit card limit",
                "loan application status",
                "investment portfolio"
            };

            foreach (var query in queries)
            {
                Console.WriteLine($"📝 Query: \"{query}\"");
                Console.WriteLine();

                var request = new SearchRequest
                {
                    Query = query,
                    TopResults = 5,
                    SearchType = SearchType.Lexical,
                    IncludeScoreBreakdown = true
                };

                var response = await lexicalSearch.SearchAsync(request);
                DisplayResults(response, "Lexical (BM25)");

                Console.WriteLine();
            }
        }

        // ============================================================
        // SEMANTIC SEARCH DEMO
        // ============================================================

        private async Task RunSemanticSearchDemoAsync(
            SemanticSearch semanticSearch,
            IEmbeddingGenerator embeddingGen)
        {
            Console.WriteLine("🔍 2. SEMANTIC SEARCH (Vector)");
            Console.WriteLine(new string('─', 80));
            Console.WriteLine();

            var queries = new[]
            {
                "How much money do I have in my account?",
                "What is my credit card usage?",
                "Can I see my recent spending?",
                "Tell me about my investments"
            };

            foreach (var query in queries)
            {
                Console.WriteLine($"📝 Query: \"{query}\"");
                Console.WriteLine();

                var queryVector = await embeddingGen.GenerateEmbeddingAsync(query);

                var request = new SearchRequest
                {
                    Query = query,
                    TopResults = 5,
                    SearchType = SearchType.Semantic,
                    IncludeScoreBreakdown = true
                };

                var response = await semanticSearch.SearchAsync(request, queryVector);
                DisplayResults(response, "Semantic (Vector)");

                Console.WriteLine();
            }
        }

        // ============================================================
        // HYBRID SEARCH DEMO
        // ============================================================

        private async Task RunHybridSearchDemoAsync(HybridSearchEngine hybridSearch)
        {
            Console.WriteLine("🔍 3. HYBRID SEARCH (RRF)");
            Console.WriteLine(new string('─', 80));
            Console.WriteLine();

            var queries = new[]
            {
                "account balance and transactions",
                "credit card and loan information",
                "investment and portfolio details"
            };

            foreach (var query in queries)
            {
                Console.WriteLine($"📝 Query: \"{query}\"");
                Console.WriteLine();

                var request = new SearchRequest
                {
                    Query = query,
                    TopResults = 5,
                    SearchType = SearchType.Hybrid,
                    IncludeScoreBreakdown = true
                };

                var response = await hybridSearch.HybridSearchAsync(request);
                DisplayResults(response, "Hybrid (RRF)");

                Console.WriteLine();
            }
        }

        // ============================================================
        // SEARCH COMPARISON DEMO
        // ============================================================

        private async Task RunSearchComparisonDemoAsync(
            HybridSearchEngine hybridSearch,
            LexicalSearch lexicalSearch,
            SemanticSearch semanticSearch,
            IEmbeddingGenerator embeddingGen)
        {
            Console.WriteLine("🔍 4. SEARCH COMPARISON");
            Console.WriteLine(new string('─', 80));
            Console.WriteLine();

            var query = "account balance and recent transactions";
            Console.WriteLine($"📝 Query: \"{query}\"");
            Console.WriteLine();

            // Lexical Search
            Console.WriteLine("   A. Lexical Search (BM25):");
            var lexicalRequest = new SearchRequest { Query = query, TopResults = 3 };
            var lexicalResponse = await lexicalSearch.SearchAsync(lexicalRequest);
            Console.WriteLine($"      Results: {lexicalResponse.TotalCount}");
            foreach (var result in lexicalResponse.Results)
            {
                Console.WriteLine($"      - {result.Title} (Score: {result.Score:F4})");
            }
            Console.WriteLine();

            // Semantic Search
            Console.WriteLine("   B. Semantic Search (Vector):");
            var queryVector = await embeddingGen.GenerateEmbeddingAsync(query);
            var semanticRequest = new SearchRequest { Query = query, TopResults = 3 };
            var semanticResponse = await semanticSearch.SearchAsync(semanticRequest, queryVector);
            Console.WriteLine($"      Results: {semanticResponse.TotalCount}");
            foreach (var result in semanticResponse.Results)
            {
                Console.WriteLine($"      - {result.Title} (Score: {result.Score:F4})");
            }
            Console.WriteLine();

            // Hybrid Search
            Console.WriteLine("   C. Hybrid Search (RRF):");
            var hybridRequest = new SearchRequest { Query = query, TopResults = 3 };
            var hybridResponse = await hybridSearch.HybridSearchAsync(hybridRequest);
            Console.WriteLine($"      Results: {hybridResponse.TotalCount}");
            foreach (var result in hybridResponse.Results)
            {
                Console.WriteLine($"      - {result.Title} (Score: {result.Score:F4})");
                if (result.ScoreBreakdown != null)
                {
                    Console.WriteLine($"        Lexical: {result.ScoreBreakdown.LexicalScore:F4}, Semantic: {result.ScoreBreakdown.SemanticScore:F4}");
                }
            }
            Console.WriteLine();

            // Comparison Summary
            Console.WriteLine("   📊 Comparison Summary:");
            Console.WriteLine($"      Lexical  - Best for exact keyword matching");
            Console.WriteLine($"      Semantic - Best for understanding user intent");
            Console.WriteLine($"      Hybrid   - Best of both worlds with RRF");
            Console.WriteLine();
        }

        // ============================================================
        // ADVANCED SEARCH DEMO
        // ============================================================

        private async Task RunAdvancedSearchDemoAsync(HybridSearchEngine hybridSearch)
        {
            Console.WriteLine("🔍 5. ADVANCED SEARCH FEATURES");
            Console.WriteLine(new string('─', 80));
            Console.WriteLine();

            // Weighted Hybrid Search
            Console.WriteLine("   A. Weighted Hybrid Search:");
            var query = "banking and account information";
            var request = new SearchRequest { Query = query, TopResults = 5 };

            var weightedResponse = await hybridSearch.HybridSearchWithWeightsAsync(
                request,
                lexicalWeight: 0.3,
                semanticWeight: 0.7);

            Console.WriteLine($"      Query: \"{query}\"");
            Console.WriteLine($"      Results: {weightedResponse.TotalCount}");
            foreach (var result in weightedResponse.Results)
            {
                Console.WriteLine($"      - {result.Title} (Score: {result.Score:F4})");
            }
            Console.WriteLine();

            // Performance Metrics
            Console.WriteLine("   B. Performance Metrics:");
            var perfRequest = new SearchRequest { Query = "account", TopResults = 10 };
            var perfResponse = await hybridSearch.HybridSearchAsync(perfRequest);

            Console.WriteLine($"      Search Method: {perfResponse.SearchMethod}");
            Console.WriteLine($"      Processing Time: {perfResponse.ProcessingTime.TotalMilliseconds:F2}ms");
            if (perfResponse.Metadata != null)
            {
                if (perfResponse.Metadata.TryGetValue("lexical_results", out var lexicalCount))
                    Console.WriteLine($"      Lexical Results: {lexicalCount}");
                if (perfResponse.Metadata.TryGetValue("semantic_results", out var semanticCount))
                    Console.WriteLine($"      Semantic Results: {semanticCount}");
                if (perfResponse.Metadata.TryGetValue("fused_results", out var fusedCount))
                    Console.WriteLine($"      Fused Results: {fusedCount}");
            }
            Console.WriteLine();
        }

        // ============================================================
        // DISPLAY HELPERS
        // ============================================================

        private void DisplayResults(SearchResponse response, string searchType)
        {
            Console.WriteLine($"   {searchType} Results:");
            Console.WriteLine($"   Total: {response.TotalCount}, Time: {response.ProcessingTime.TotalMilliseconds:F2}ms");
            Console.WriteLine();

            foreach (var result in response.Results)
            {
                Console.WriteLine($"   [{result.Id}] {result.Title}");
                Console.WriteLine($"   Score: {result.Score:F4}");

                if (result.ScoreBreakdown != null)
                {
                    Console.WriteLine($"   └─ Lexical: {result.ScoreBreakdown.LexicalScore:F4}, Semantic: {result.ScoreBreakdown.SemanticScore:F4}");
                }

                Console.WriteLine($"   └─ Preview: {(result.Content.Length > 80 ? result.Content[..80] + "..." : result.Content)}");
                Console.WriteLine();
            }
        }
    }
}