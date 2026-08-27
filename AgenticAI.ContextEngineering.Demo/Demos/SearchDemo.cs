// SearchDemo.cs - Updated to implement IDemo
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Search;
using AgenticAI.ContextEngineering.Demo.Demos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public class SearchDemo : IDemo
    {
        private readonly LexicalSearch _lexicalSearch;
        private readonly SemanticSearch _semanticSearch;
        private readonly HybridSearchEngine _hybridSearchEngine;
        private readonly SearchIndex _searchIndex;
        private readonly IQdrantClient? _qdrantClient;
        private readonly IEmbeddingGenerator _embeddingGenerator;
        private readonly ILogger<SearchDemo> _logger;

        // IDemo Implementation
        public string Name => "Search Demo";
        public string Description => "Tests Lexical, Semantic, and Hybrid Search with RRF";
        public bool IsConfigured => _searchIndex != null && _lexicalSearch != null && _semanticSearch != null;
        public string ConfigurationStatus => IsConfigured ? "✅ Configured" : "❌ Not Configured";

        public SearchDemo(
            LexicalSearch lexicalSearch,
            SemanticSearch semanticSearch,
            HybridSearchEngine hybridSearchEngine,
            SearchIndex searchIndex,
            IQdrantClient? qdrantClient,
            IEmbeddingGenerator embeddingGenerator,
            ILogger<SearchDemo> logger)
        {
            _lexicalSearch = lexicalSearch;
            _semanticSearch = semanticSearch;
            _hybridSearchEngine = hybridSearchEngine;
            _searchIndex = searchIndex;
            _qdrantClient = qdrantClient;
            _embeddingGenerator = embeddingGenerator;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  🔍 Search Demo - Lexical | Semantic | Hybrid Search with RRF               ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            try
            {
                // Index sample documents (in-memory only - fast)
                await IndexSampleDocumentsAsync();

                // Run search demos using in-memory index (no Qdrant dependency)
                await RunLexicalSearchDemoAsync();

                await RunSemanticSearchDemoAsync();

                await RunHybridSearchDemoAsync();

                await RunSearchComparisonDemoAsync();

                await RunFilteredSearchDemoAsync();

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Search Demo Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }
            }
        }

        private async Task IndexSampleDocumentsAsync()
        {
            Console.WriteLine("📝 Indexing Sample Documents...");
            Console.WriteLine("────────────────────────────────────────────────────────────────────────────────");
            Console.WriteLine();

            // Sample documents
            var documents = GetSampleDocuments();

            // Index in memory
            _searchIndex.Clear();
            _searchIndex.IndexDocuments(documents);
            Console.WriteLine($"✅ Indexed {documents.Count} documents to memory");

            // Try to index to Qdrant with timeout (optional, non-blocking)
            if (_qdrantClient != null)
            {
                Console.WriteLine();
                Console.WriteLine("📤 Indexing documents to Qdrant (optional)...");
                Console.WriteLine("────────────────────────────────────────────────────────────────────────────────");

                try
                {
                    // Use a cancellation token with timeout
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                    foreach (var doc in documents)
                    {
                        try
                        {
                            var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(doc.Content);
                            await _qdrantClient.IndexDocumentAsync(
                                doc.Id,
                                embedding,
                                doc.Content,
                                doc.Title,
                                doc.Source ?? "Unknown",
                                doc.Metadata,
                                cts.Token
                            );
                            Console.WriteLine($"   ✅ Indexed: {doc.Title}");
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine($"   ⏱️ Timeout indexing: {doc.Title} (skipping)");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"   ⚠️ Failed to index: {doc.Title} - {ex.Message}");
                        }
                    }

                    try
                    {
                        var size = await _qdrantClient.GetCollectionSizeAsync();
                        Console.WriteLine($"\n📊 Qdrant collection has {size} documents");
                    }
                    catch { /* Ignore */ }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️ Qdrant indexing error: {ex.Message}");
                    Console.WriteLine("   Continuing with in-memory search only...");
                }
            }
            else
            {
                Console.WriteLine("   ℹ️ Qdrant not configured - using in-memory search only");
            }

            // Display indexed documents
            Console.WriteLine();
            Console.WriteLine("📚 Indexed Documents:");
            foreach (var doc in documents)
            {
                var preview = doc.Content.Length > 50 ? doc.Content.Substring(0, 50) + "..." : doc.Content;
                Console.WriteLine($"   [{doc.Id}] {doc.Title}");
                Console.WriteLine($"      {preview}");
                Console.WriteLine($"      Source: {doc.Source ?? "Unknown"}");
                Console.WriteLine($"      Length: {doc.Content.Length} chars");
                Console.WriteLine();
            }
        }

        private List<Document> GetSampleDocuments()
        {
            return new List<Document>
            {
                new Document
                {
                    Id = "doc1",
                    Title = "Bank Account Balance",
                    Content = "Your bank account balance is $1,234.56. You have 5 transactions this month. The current balance includes pending transactions.",
                    Source = "Banking API",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Category"] = "banking",
                        ["Priority"] = "high",
                        ["Type"] = "account"
                    }
                },
                new Document
                {
                    Id = "doc2",
                    Title = "Recent Transactions",
                    Content = "Recent transactions: Jan 15: -$50.00 at Grocery Store, Jan 10: -$25.00 at Coffee Shop, Jan 5: +$500.00 Payroll Deposit",
                    Source = "Transaction Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Category"] = "transactions",
                        ["Priority"] = "medium",
                        ["Type"] = "history"
                    }
                },
                new Document
                {
                    Id = "doc3",
                    Title = "Pending Payments",
                    Content = "Pending payments: $75.00 due on Feb 1 for Utility Bill, $150.00 due on Feb 15 for Credit Card, $200.00 due on Mar 1 for Rent",
                    Source = "Payment Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Category"] = "payments",
                        ["Priority"] = "high",
                        ["Type"] = "pending"
                    }
                },
                new Document
                {
                    Id = "doc4",
                    Title = "Account Statement",
                    Content = "Monthly statement: Opening balance $1,200.00, Closing balance $1,234.56, Total deposits $2,500.00, Total withdrawals $2,465.44",
                    Source = "Statement Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Category"] = "statements",
                        ["Priority"] = "medium",
                        ["Type"] = "account"
                    }
                },
                new Document
                {
                    Id = "doc5",
                    Title = "Credit Card Limit",
                    Content = "Credit card limit is $5,000.00. Current usage is $2,345.67. Available credit is $2,654.33. Next statement date is Feb 1.",
                    Source = "Card Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Category"] = "credit",
                        ["Priority"] = "medium",
                        ["Type"] = "card"
                    }
                },
                new Document
                {
                    Id = "doc6",
                    Title = "Loan Application Status",
                    Content = "Your loan application for $25,000 is currently under review. The estimated decision date is Feb 15. Please check your email for updates.",
                    Source = "Loan Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Category"] = "loans",
                        ["Priority"] = "high",
                        ["Type"] = "application"
                    }
                },
                new Document
                {
                    Id = "doc7",
                    Title = "Investment Portfolio",
                    Content = "Your investment portfolio summary: Stocks: $15,000 (60%), Bonds: $5,000 (20%), Cash: $5,000 (20%). Total portfolio value: $25,000.",
                    Source = "Investment Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Category"] = "investments",
                        ["Priority"] = "medium",
                        ["Type"] = "portfolio"
                    }
                },
                new Document
                {
                    Id = "doc8",
                    Title = "Account Settings",
                    Content = "Account settings: 2FA is enabled, Email notifications are on, Monthly statements are sent via email, Contact number is updated.",
                    Source = "Settings Service",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Category"] = "settings",
                        ["Priority"] = "low",
                        ["Type"] = "preferences"
                    }
                }
            };
        }

        private async Task RunLexicalSearchDemoAsync()
        {
            Console.WriteLine();
            Console.WriteLine("🔍 1. LEXICAL SEARCH (BM25)");
            Console.WriteLine("────────────────────────────────────────────────────────────────────────────────");

            var queries = new[]
            {
                "account balance and transactions",
                "credit card limit",
                "loan application status",
                "investment portfolio"
            };

            foreach (var query in queries)
            {
                Console.WriteLine();
                Console.WriteLine($"📝 Query: \"{query}\"");

                var request = new SearchRequest
                {
                    Query = query,
                    TopResults = 5,
                    IncludeScoreBreakdown = true
                };

                var stopwatch = Stopwatch.StartNew();
                var response = await _lexicalSearch.SearchAsync(request);
                stopwatch.Stop();

                var hasResults = response != null && response.Results != null;
                var resultCount = hasResults ? response.Results.Count : 0;
                var timeMs = stopwatch.Elapsed.TotalMilliseconds;

                Console.WriteLine($"\n   Lexical (BM25) Results:");
                Console.WriteLine($"   Total: {resultCount}, Time: {timeMs:F2}ms");

                if (hasResults && response.Results.Any())
                {
                    foreach (var result in response.Results.Take(5))
                    {
                        var title = string.IsNullOrEmpty(result.Title) ? "Untitled" : result.Title;
                        var preview = result.Content.Length > 60 ? result.Content.Substring(0, 60) + "..." : result.Content;
                        var lexicalScore = result.ScoreBreakdown?.LexicalScore ?? 0;
                        var semanticScore = result.ScoreBreakdown?.SemanticScore ?? 0;

                        Console.WriteLine($"\n   [{result.Id}] {title}");
                        Console.WriteLine($"   Score: {result.Score:F4}");
                        Console.WriteLine($"   └─ Lexical: {lexicalScore:F4}, Semantic: {semanticScore:F4}");
                        Console.WriteLine($"   └─ Preview: {preview}");
                    }
                }
            }
        }

        private async Task RunSemanticSearchDemoAsync()
        {
            Console.WriteLine();
            Console.WriteLine("🔍 2. SEMANTIC SEARCH (Vector)");
            Console.WriteLine("────────────────────────────────────────────────────────────────────────────────");

            var queries = new[]
            {
                "How much money do I have in my account?",
                "What is my credit card usage?",
                "Can I see my recent spending?",
                "Tell me about my investments"
            };

            foreach (var query in queries)
            {
                Console.WriteLine();
                Console.WriteLine($"📝 Query: \"{query}\"");

                var queryVector = await _embeddingGenerator.GenerateEmbeddingAsync(query);
                Console.WriteLine($"\n   ✅ Query vector generated: {queryVector.Length} dimensions");

                var request = new SearchRequest
                {
                    Query = query,
                    TopResults = 5,
                    MinimumRelevanceScore = 0.0
                };

                var stopwatch = Stopwatch.StartNew();
                var response = await _semanticSearch.SearchAsync(request, queryVector);
                stopwatch.Stop();

                var hasResults = response != null && response.Results != null;
                var resultCount = hasResults ? response.Results.Count : 0;
                var timeMs = stopwatch.Elapsed.TotalMilliseconds;

                Console.WriteLine($"\n   Semantic (Vector) Results:");
                Console.WriteLine($"   Total: {resultCount}, Time: {timeMs:F2}ms");

                if (hasResults && response.Results.Any())
                {
                    foreach (var result in response.Results.Take(5))
                    {
                        var title = string.IsNullOrEmpty(result.Title) ? "Untitled" : result.Title;
                        var preview = result.Content.Length > 60 ? result.Content.Substring(0, 60) + "..." : result.Content;

                        Console.WriteLine($"\n   [{result.Id}] {title}");
                        Console.WriteLine($"   Score: {result.Score:F4}");
                        Console.WriteLine($"   └─ Preview: {preview}");
                    }
                }
            }
        }

        private async Task RunHybridSearchDemoAsync()
        {
            Console.WriteLine();
            Console.WriteLine("🔍 3. HYBRID SEARCH (RRF)");
            Console.WriteLine("────────────────────────────────────────────────────────────────────────────────");

            var queries = new[]
            {
                "account balance and transactions",
                "credit card and loan information",
                "investment and portfolio details"
            };

            foreach (var query in queries)
            {
                Console.WriteLine();
                Console.WriteLine($"📝 Query: \"{query}\"");

                var request = new SearchRequest
                {
                    Query = query,
                    TopResults = 5,
                    IncludeScoreBreakdown = true
                };

                var stopwatch = Stopwatch.StartNew();
                var response = await _hybridSearchEngine.HybridSearchAsync(request);
                stopwatch.Stop();

                var hasResults = response != null && response.Results != null;
                var resultCount = hasResults ? response.Results.Count : 0;
                var timeMs = stopwatch.Elapsed.TotalMilliseconds;

                Console.WriteLine($"\n   Hybrid (RRF) Results:");
                Console.WriteLine($"   Total: {resultCount}, Time: {timeMs:F2}ms");

                if (hasResults && response.Results.Any())
                {
                    foreach (var result in response.Results.Take(5))
                    {
                        var title = string.IsNullOrEmpty(result.Title) ? "Untitled" : result.Title;
                        var preview = result.Content.Length > 60 ? result.Content.Substring(0, 60) + "..." : result.Content;
                        var lexicalScore = result.ScoreBreakdown?.LexicalScore ?? 0;
                        var semanticScore = result.ScoreBreakdown?.SemanticScore ?? 0;

                        Console.WriteLine($"\n   [{result.Id}] {title}");
                        Console.WriteLine($"   Score: {result.Score:F4}");
                        Console.WriteLine($"   └─ Lexical: {lexicalScore:F4}, Semantic: {semanticScore:F4}");
                        Console.WriteLine($"   └─ Preview: {preview}");
                    }
                }
            }
        }

        private async Task RunSearchComparisonDemoAsync()
        {
            Console.WriteLine();
            Console.WriteLine("🔍 4. SEARCH COMPARISON");
            Console.WriteLine("────────────────────────────────────────────────────────────────────────────────");

            var query = "account balance and recent transactions";
            Console.WriteLine($"📝 Query: \"{query}\"");
            Console.WriteLine();

            // Lexical Search
            var lexicalRequest = new SearchRequest
            {
                Query = query,
                TopResults = 3,
                IncludeScoreBreakdown = true
            };
            var lexicalResponse = await _lexicalSearch.SearchAsync(lexicalRequest);

            Console.WriteLine("   A. Lexical Search (BM25):");
            var hasLexicalResults = lexicalResponse != null && lexicalResponse.Results != null;
            var lexicalCount = hasLexicalResults ? lexicalResponse.Results.Count : 0;
            Console.WriteLine($"      Results: {lexicalCount}");
            if (hasLexicalResults && lexicalResponse.Results.Any())
            {
                foreach (var result in lexicalResponse.Results)
                {
                    var title = string.IsNullOrEmpty(result.Title) ? "Untitled" : result.Title;
                    Console.WriteLine($"      - {title} (Score: {result.Score:F4})");
                }
            }
            Console.WriteLine();

            // Semantic Search
            var queryVector = await _embeddingGenerator.GenerateEmbeddingAsync(query);
            var semanticRequest = new SearchRequest
            {
                Query = query,
                TopResults = 3,
                MinimumRelevanceScore = 0.0
            };
            var semanticResponse = await _semanticSearch.SearchAsync(semanticRequest, queryVector);

            Console.WriteLine("   B. Semantic Search (Vector):");
            var hasSemanticResults = semanticResponse != null && semanticResponse.Results != null;
            var semanticCount = hasSemanticResults ? semanticResponse.Results.Count : 0;
            Console.WriteLine($"      Results: {semanticCount}");
            if (hasSemanticResults && semanticResponse.Results.Any())
            {
                foreach (var result in semanticResponse.Results.Take(3))
                {
                    var title = string.IsNullOrEmpty(result.Title) ? "Untitled" : result.Title;
                    Console.WriteLine($"      - {title} (Score: {result.Score:F4})");
                }
            }
            Console.WriteLine();

            // Hybrid Search
            var hybridRequest = new SearchRequest
            {
                Query = query,
                TopResults = 3,
                IncludeScoreBreakdown = true
            };
            var hybridResponse = await _hybridSearchEngine.HybridSearchAsync(hybridRequest);

            Console.WriteLine("   C. Hybrid Search (RRF):");
            var hasHybridResults = hybridResponse != null && hybridResponse.Results != null;
            var hybridCount = hasHybridResults ? hybridResponse.Results.Count : 0;
            Console.WriteLine($"      Results: {hybridCount}");
            if (hasHybridResults && hybridResponse.Results.Any())
            {
                foreach (var result in hybridResponse.Results)
                {
                    var title = string.IsNullOrEmpty(result.Title) ? "Untitled" : result.Title;
                    Console.WriteLine($"      - {title} (Score: {result.Score:F4})");
                    var hasScoreBreakdown = result.ScoreBreakdown != null;
                    if (hasScoreBreakdown)
                    {
                        Console.WriteLine($"        Lexical: {result.ScoreBreakdown.LexicalScore:F4}, Semantic: {result.ScoreBreakdown.SemanticScore:F4}");
                    }
                }
            }
            Console.WriteLine();

            Console.WriteLine("   📊 Comparison Summary:");
            Console.WriteLine("      Lexical  - Best for exact keyword matching");
            Console.WriteLine("      Semantic - Best for understanding user intent");
            Console.WriteLine("      Hybrid   - Best of both worlds with RRF");
        }

        private async Task RunFilteredSearchDemoAsync()
        {
            Console.WriteLine();
            Console.WriteLine("🔍 5. FILTERED SEARCH");
            Console.WriteLine("────────────────────────────────────────────────────────────────────────────────");

            var query = "account information";
            Console.WriteLine($"📝 Query: \"{query}\"");
            Console.WriteLine();

            // Filter by Category
            Console.WriteLine("   A. Filter by Category 'banking':");
            var categoryRequest = new SearchRequest
            {
                Query = query,
                TopResults = 5,
                Filters = new Dictionary<string, object>
                {
                    ["Category"] = "banking"
                }
            };
            var categoryResponse = await _lexicalSearch.SearchAsync(categoryRequest);

            var hasCategoryResults = categoryResponse != null && categoryResponse.Results != null;
            var categoryCount = hasCategoryResults ? categoryResponse.Results.Count : 0;
            Console.WriteLine($"      Results: {categoryCount}");
            if (hasCategoryResults && categoryResponse.Results.Any())
            {
                foreach (var result in categoryResponse.Results)
                {
                    var title = string.IsNullOrEmpty(result.Title) ? "Untitled" : result.Title;
                    var category = result.Metadata?.GetValueOrDefault("Category")?.ToString() ?? "unknown";
                    Console.WriteLine($"      - {title} (Score: {result.Score:F4})");
                    Console.WriteLine($"        Category: {category}");
                }
            }
            Console.WriteLine();

            // Filter by Priority
            Console.WriteLine("   B. Filter by Priority 'high':");
            var priorityRequest = new SearchRequest
            {
                Query = query,
                TopResults = 5,
                Filters = new Dictionary<string, object>
                {
                    ["Priority"] = "high"
                }
            };
            var priorityResponse = await _lexicalSearch.SearchAsync(priorityRequest);

            var hasPriorityResults = priorityResponse != null && priorityResponse.Results != null;
            var priorityCount = hasPriorityResults ? priorityResponse.Results.Count : 0;
            Console.WriteLine($"      Results: {priorityCount}");
            if (hasPriorityResults && priorityResponse.Results.Any())
            {
                foreach (var result in priorityResponse.Results)
                {
                    var title = string.IsNullOrEmpty(result.Title) ? "Untitled" : result.Title;
                    var priority = result.Metadata?.GetValueOrDefault("Priority")?.ToString() ?? "unknown";
                    Console.WriteLine($"      - {title} (Score: {result.Score:F4})");
                    Console.WriteLine($"        Priority: {priority}");
                }
            }
        }
    }
}