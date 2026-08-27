// AgenticAI.ContextEngineering.Core/Services/AIResponseService.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class AIResponseService : IAIResponseService
    {
        private readonly ChatClient? _chatClient;
        private readonly IOptions<AIResponseOptions> _options;
        private readonly ILogger<AIResponseService> _logger;
        private readonly bool _isConfigured;
        private readonly bool _useMockResponses;

        public AIResponseService(
            AzureOpenAIClient? openAIClient,
            IOptions<AIResponseOptions> options,
            ILogger<AIResponseService> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Check if we have valid credentials
            var hasEndpoint = !string.IsNullOrEmpty(_options.Value.Endpoint);
            var hasApiKey = !string.IsNullOrEmpty(_options.Value.ApiKey);
            var hasDeployment = !string.IsNullOrEmpty(_options.Value.DeploymentName);

            if (hasEndpoint && hasApiKey && hasDeployment)
            {
                try
                {
                    if (openAIClient != null)
                    {
                        var deploymentName = _options.Value.DeploymentName ?? "gpt-4";
                        _chatClient = openAIClient.GetChatClient(deploymentName);
                        _isConfigured = true;
                        _useMockResponses = false;
                        _logger.LogInformation("✅ Azure OpenAI client initialized");
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ AzureOpenAIClient is null, using mock responses");
                        _isConfigured = false;
                        _useMockResponses = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to initialize Azure OpenAI client, using mock responses");
                    _isConfigured = false;
                    _useMockResponses = true;
                }
            }
            else
            {
                _logger.LogWarning("⚠️ Azure OpenAI credentials not fully configured. Using mock responses.");
                _logger.LogWarning($"   Endpoint: {(hasEndpoint ? "✅" : "❌")}");
                _logger.LogWarning($"   API Key: {(hasApiKey ? "✅" : "❌")}");
                _logger.LogWarning($"   Deployment: {(hasDeployment ? "✅" : "❌")}");
                _isConfigured = false;
                _useMockResponses = true;
            }
        }

        public async Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Use mock responses if not configured
                if (_useMockResponses || !_isConfigured || _chatClient == null)
                {
                    _logger.LogDebug($"Using mock response for: {request.UserQuery}");
                    var mockResponse = GenerateMockResponse(request.UserQuery);

                    // Simulate processing delay
                    await Task.Delay(100, cancellationToken);

                    return new AIResponseResult
                    {
                        Response = mockResponse,
                        Answer = mockResponse,
                        IsSuccess = true,
                        Query = request.UserQuery,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        TokenCount = mockResponse.Split(' ').Length + 10,
                        PromptTokens = 20,
                        CompletionTokens = mockResponse.Split(' ').Length,
                        FromCache = false,
                        CacheLevel = "MockGenerated"
                    };
                }

                _logger.LogDebug("Generating response for: {Query}", request.UserQuery);


                var messages = new List<OpenAI.Chat.ChatMessage>();

                var systemPrompt = BuildSystemPrompt(request);
                messages.Add(new SystemChatMessage(systemPrompt));

  
                if (request.ConversationHistory?.Any() == true)
                {
                    foreach (var msg in request.ConversationHistory.TakeLast(10))
                    {
                        if (string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                            messages.Add(new UserChatMessage(msg.Content));
                        else if (string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                            messages.Add(new AssistantChatMessage(msg.Content));
                    }
                }

                // 3. Add current user query
                messages.Add(new UserChatMessage(request.UserQuery));

                var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
                var completion = response.Value;

                var responseText = completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;

                return new AIResponseResult
                {
                    Response = responseText,
                    Answer = responseText,
                    PromptTokens = completion.Usage?.InputTokenCount ?? 0,
                    CompletionTokens = completion.Usage?.OutputTokenCount ?? 0,
                    TokenCount = completion.Usage?.TotalTokenCount ?? 0,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    Query = request.UserQuery,
                    IsSuccess = true,
                    FromCache = false,
                    CacheLevel = "Generated"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response for: {Query}", request.UserQuery);
                return new AIResponseResult
                {
                    Error = ex.Message,
                    IsSuccess = false,
                    Query = request.UserQuery,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    FromCache = false
                };
            }
        }

        public async IAsyncEnumerable<AIStreamChunk> GenerateStreamingResponseAsync(
            AIResponseRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_useMockResponses || !_isConfigured || _chatClient == null)
            {
                var mockResponse = GenerateMockResponse(request.UserQuery);
                var words = mockResponse.Split(' ');

                for (int i = 0; i < words.Length; i++)
                {
                    yield return new AIStreamChunk
                    {
                        Content = words[i] + (i < words.Length - 1 ? " " : ""),
                        IsComplete = false
                    };

                    // Simulate streaming delay
                    await Task.Delay(20, cancellationToken);
                }

                yield return new AIStreamChunk { IsComplete = true };
                yield break;
            }

            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage(_options.Value.SystemPrompt ?? "You are a helpful assistant.")
            };

            if (request.ConversationHistory?.Any() == true)
            {
                foreach (var msg in request.ConversationHistory.TakeLast(10))
                {
                    if (string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                        messages.Add(new UserChatMessage(msg.Content));
                    else if (string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                        messages.Add(new AssistantChatMessage(msg.Content));
                }
            }

            messages.Add(new UserChatMessage(request.UserQuery));

            var responseStream = _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken);

            await foreach (var update in responseStream)
            {
                foreach (var content in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(content.Text))
                    {
                        yield return new AIStreamChunk
                        {
                            Content = content.Text,
                            IsComplete = false
                        };
                    }
                }
            }

            yield return new AIStreamChunk { IsComplete = true };
        }

        public TokenUsageStats GetTokenStats() => new TokenUsageStats();

        public Task ClearTokenCacheAsync()
        {
            _logger.LogInformation("Token cache cleared");
            return Task.CompletedTask;
        }

        public Task<string> GenerateTokenReportAsync()
        {
            return Task.FromResult(_useMockResponses
                ? "⚠️ Using mock responses - Azure OpenAI not configured"
                : "Simple AI Service - No token tracking");
        }

        private string GenerateMockResponse(string query)
        {
            var lowerQuery = query.ToLowerInvariant();

            var responses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["what is artificial intelligence"] = "Artificial intelligence (AI) is the simulation of human intelligence in machines that are programmed to think and learn like humans. The term may also be applied to any machine that exhibits traits associated with a human mind such as learning and problem-solving.",
                ["explain machine learning"] = "Machine learning is a subset of artificial intelligence that enables systems to learn and improve from experience without being explicitly programmed. It uses algorithms to find patterns in data and make predictions or decisions.",
                ["difference between ai and ml"] = "AI is the broader concept of machines being able to carry out tasks in a way that we would consider 'smart'. Machine learning is a current application of AI based on the idea that we should be able to give machines access to data and let them learn for themselves.",
                ["meaning of life"] = "The meaning of life is subjective and varies from person to person. Some find meaning in relationships, others in work, spirituality, or personal growth. The search for meaning is a fundamental human experience.",
                ["quantum computing"] = "Quantum computing is a type of computing that harnesses the principles of quantum mechanics to process information. It uses quantum bits (qubits) that can exist in multiple states simultaneously, allowing for unprecedented computational power.",
                ["capital of france"] = "The capital of France is Paris. It is the country's largest city and a major global center for art, fashion, gastronomy, and culture.",
                ["renewable energy"] = "Renewable energy comes from natural sources that are constantly replenished, such as sunlight, wind, rain, tides, waves, and geothermal heat. These sources are sustainable and have a much lower environmental impact than fossil fuels.",
                ["neural networks"] = "Neural networks are computing systems inspired by biological neural networks. They consist of interconnected nodes (neurons) that process information using connectionist approaches to computation. They are a key component of deep learning.",
                ["deep learning"] = "Deep learning is a subset of machine learning that uses neural networks with multiple layers (deep neural networks) to progressively extract higher-level features from raw input. It has been highly successful in areas like computer vision and natural language processing.",
                ["natural language processing"] = "Natural Language Processing (NLP) is a branch of AI that helps computers understand, interpret, and manipulate human language. It combines computational linguistics with statistical and machine learning models."
            };

            foreach (var key in responses.Keys)
            {
                if (lowerQuery.Contains(key))
                {
                    return responses[key];
                }
            }

            // Return a generic response
            var baseResponse = $"I understand you're asking about '{query}'. ";
            if (!_useMockResponses)
            {
                return baseResponse + "This is a simulated response since Azure OpenAI is not properly configured. Please add your API key to appsettings.json for real AI responses.";
            }
            return baseResponse + "This is a mock response for testing purposes. The AI Response Service is working correctly with mock data.";
        }

        private string BuildSystemPrompt(AIResponseRequest request)
        {
            var systemPrompt = request.SystemPrompt ?? _options.Value.SystemPrompt ?? "You are a helpful assistant.";

            // ✅ Add knowledge base context if available
            if (request.ModuleData != null && request.ModuleData.TryGetValue("KnowledgeBase", out var knowledge))
            {
                systemPrompt += $"\n\nPGM KNOWLEDGE:\n---\n{knowledge}\n---\n";
            }

            // ✅ Add division context
            if (!string.IsNullOrEmpty(request.Division))
            {
                systemPrompt += $"\n\nCurrent Division: {request.Division}";
            }

            // ✅ Add user context
            if (request.ModuleData != null)
            {
                if (request.ModuleData.TryGetValue("UserLanguage", out var language))
                {
                    systemPrompt += $"\nUser Language: {language}";
                }
            }

            return systemPrompt;
        }
    }
}