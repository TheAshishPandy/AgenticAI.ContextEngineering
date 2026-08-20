//// TestAIResponseService.cs - Console Demo
//using AgenticAI.ContextEngineering.Core.Models;
//using AgenticAI.ContextEngineering.Core.Services;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;
//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;

//namespace AgenticAI.Test
//{
//    public class Program
//    {
//        public static async Task Main(string[] args)
//        {
//            Console.WriteLine("=== AIResponseService Test Demo ===\n");

//            // Setup configuration
//            var configuration = new ConfigurationBuilder()
//                .AddInMemoryCollection(new Dictionary<string, string>
//                {
//                    ["AzureOpenAIEndpoint"] = "https://scmv11.openai.azure.com/",
//                    ["AzureOpenAIKey"] = "YOUR-API-KEY-HERE",
//                    ["AzureOpenAIDeploymentName"] = "wechat"
//                })
//                .Build();

//            // Setup services
//            var services = new ServiceCollection();

//            // Add logging
//            services.AddLogging(builder =>
//            {
//                builder.AddConsole();
//                builder.SetMinimumLevel(LogLevel.Debug);
//            });

//            // Add AI Response Service with options
//            services.AddSingleton<IConfiguration>(configuration);
//            services.AddOptions<AIResponseOptions>()
//                .Configure(options =>
//                {
//                    options.AzureOpenAIEndpoint = configuration["AzureOpenAIEndpoint"];
//                    options.AzureOpenAIKey = configuration["AzureOpenAIKey"];
//                    options.AzureOpenAIDeploymentName = configuration["AzureOpenAIDeploymentName"];
//                    options.DefaultTemperature = 0.2f;
//                    options.DefaultMaxTokens = 500;
//                    options.SummaryThreshold = 15;
//                    options.MaxHistoryMessages = 20;
//                    options.RemoveUrlsFromResponse = true;
//                    options.PrioritizeDynamicData = true;
//                });

//            services.AddSingleton<AIResponseService>();

//            var serviceProvider = services.BuildServiceProvider();
//            var aiResponseService = serviceProvider.GetRequiredService<AIResponseService>();

//            // Test 1: Simple query
//            await TestSimpleQuery(aiResponseService);

//            // Test 2: Query with module data
//            await TestQueryWithModuleData(aiResponseService);

//            // Test 3: Query with conversation history
//            await TestQueryWithConversationHistory(aiResponseService);

//            Console.WriteLine("\n=== All tests completed ===");
//            Console.ReadKey();
//        }

//        static async Task TestSimpleQuery(AIResponseService service)
//        {
//            Console.WriteLine("\n--- Test 1: Simple Query ---");

//            var request = new AIResponseRequest
//            {
//                ConversationId = "test-conv-001",
//                Division = "PGW",
//                UserQuery = "Hello! Can you help me with my bill?",
//                ModuleData = new Dictionary<string, string>(),
//                Temperature = 0.2f,
//                MaxTokens = 500,
//                RemoveUrlsFromResponse = true,
//                PrioritizeDynamicData = true
//            };

//            try
//            {
//                Console.WriteLine($"Query: {request.UserQuery}");
//                var result = await service.GenerateResponseAsync(request);

//                Console.WriteLine($"Response: {result.Response}");
//                Console.WriteLine($"Token Count: {result.TokenCount}");
//                Console.WriteLine($"Prompt Tokens: {result.PromptTokens}");
//                Console.WriteLine($"Completion Tokens: {result.CompletionTokens}");
//                Console.WriteLine($"Used Summary: {result.UsedSummary}");
//                Console.WriteLine($"URLs Removed: {result.HadUrlsRemoved}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error: {ex.Message}");
//            }
//        }

//        static async Task TestQueryWithModuleData(AIResponseService service)
//        {
//            Console.WriteLine("\n--- Test 2: Query with Module Data ---");

//            var request = new AIResponseRequest
//            {
//                ConversationId = "test-conv-002",
//                Division = "PGW",
//                UserQuery = "What is my current balance?",
//                ModuleData = new Dictionary<string, string>
//                {
//                    ["CurrentBalance"] = "$150.75",
//                    ["DueDate"] = "2024-12-15",
//                    ["PastDueAmount"] = "$0.00",
//                    ["AccountNumber"] = "123456789",
//                    ["ServiceAddress"] = "123 Main St, City, State 12345"
//                },
//                Temperature = 0.2f,
//                MaxTokens = 500,
//                RemoveUrlsFromResponse = true,
//                PrioritizeDynamicData = true,
//                DynamicDataKeywords = new List<string> { "current", "balance", "due", "amount" }
//            };

//            try
//            {
//                Console.WriteLine($"Query: {request.UserQuery}");
//                Console.WriteLine($"Module Data: {string.Join(", ", request.ModuleData)}");
//                var result = await service.GenerateResponseAsync(request);

//                Console.WriteLine($"Response: {result.Response}");
//                Console.WriteLine($"Token Count: {result.TokenCount}");
//                Console.WriteLine($"Prompt Tokens: {result.PromptTokens}");
//                Console.WriteLine($"Completion Tokens: {result.CompletionTokens}");
//                Console.WriteLine($"Dynamic Data Used: {!string.IsNullOrEmpty(result.DynamicDataUsed)}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error: {ex.Message}");
//            }
//        }

//        static async Task TestQueryWithConversationHistory(AIResponseService service)
//        {
//            Console.WriteLine("\n--- Test 3: Query with Conversation History ---");

//            var conversationHistory = new List<ConversationMessage>
//            {
//                new ConversationMessage { Role = "User", Content = "I need help with my bill", Timestamp = DateTime.Now.AddMinutes(-5) },
//                new ConversationMessage { Role = "Assistant", Content = "Sure, I can help with your bill. What would you like to know?", Timestamp = DateTime.Now.AddMinutes(-4) },
//                new ConversationMessage { Role = "User", Content = "What is my current balance?", Timestamp = DateTime.Now.AddMinutes(-3) }
//            };

//            var request = new AIResponseRequest
//            {
//                ConversationId = "test-conv-003",
//                Division = "PGW",
//                UserQuery = "Can you tell me the due date?",
//                ModuleData = new Dictionary<string, string>
//                {
//                    ["CurrentBalance"] = "$150.75",
//                    ["DueDate"] = "2024-12-15"
//                },
//                ConversationHistory = conversationHistory,
//                Temperature = 0.2f,
//                MaxTokens = 500,
//                RemoveUrlsFromResponse = true,
//                PrioritizeDynamicData = true,
//                DynamicDataKeywords = new List<string> { "current", "balance", "due", "date" }
//            };

//            try
//            {
//                Console.WriteLine($"Query: {request.UserQuery}");
//                Console.WriteLine($"History Count: {conversationHistory.Count}");
//                var result = await service.GenerateResponseAsync(request);

//                Console.WriteLine($"Response: {result.Response}");
//                Console.WriteLine($"Token Count: {result.TokenCount}");
//                Console.WriteLine($"Prompt Tokens: {result.PromptTokens}");
//                Console.WriteLine($"Completion Tokens: {result.CompletionTokens}");
//                Console.WriteLine($"Used Summary: {result.UsedSummary}");
//                Console.WriteLine($"Dynamic Data Used: {!string.IsNullOrEmpty(result.DynamicDataUsed)}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error: {ex.Message}");
//            }
//        }
//    }
//}