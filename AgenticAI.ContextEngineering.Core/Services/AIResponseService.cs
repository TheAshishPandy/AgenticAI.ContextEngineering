// AgenticAI.ContextEngineering.Core/Services/AIResponseService.cs
using AgenticAI.ContextEngineering.Core.Helpers;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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

        private const int MAX_TOKENS_LIMIT = 4000;
        private const int WARNING_TOKEN_THRESHOLD = 1500;

        public AIResponseService(
            AzureOpenAIClient? openAIClient,
            IOptions<AIResponseOptions> options,
            ILogger<AIResponseService> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
                _isConfigured = false;
                _useMockResponses = true;
            }
        }

        public async Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (_useMockResponses || !_isConfigured || _chatClient == null)
                {
                    var mockResponse = GenerateMockResponse(request.UserQuery);
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

                // ✅ Detect intent using IntentHelper
                var intentType = IntentHelper.DetectIntent(request.UserQuery);
                request.IntentType = intentType;
                _logger.LogInformation($"🎯 Intent detected: {intentType} for query: {request.UserQuery}");

                // ✅ Build context-aware query if it's a follow-up
                if ((intentType == "confirmation" || intentType == "followup" || intentType == "negation")
                    && !string.IsNullOrEmpty(request.LastBotQuestion))
                {
                    var contextAwareQuery = IntentHelper.BuildContextAwareQuery(
                        request.UserQuery,
                        request.LastBotQuestion,
                        intentType
                    );
                    _logger.LogInformation($"💡 Context-aware query: {contextAwareQuery}");

                    // Store original query and use enhanced query for processing
                    request.Query = request.UserQuery;
                    request.UserQuery = contextAwareQuery;
                    request.IsFollowUp = true;
                }

                BuildConversationContext(request);

                var messages = new List<OpenAI.Chat.ChatMessage>();

                var systemPrompt = BuildSystemPrompt(request);

                var maxTokens = request.MaxTokens;
                var history = request.ConversationHistory?.ToList() ?? new List<ConversationMessage>();

                var breakdown = TokenCounter.GetTokenBreakdown(
                    systemPrompt: systemPrompt,
                    query: request.UserQuery,
                    history: history,
                    moduleData: request.ModuleData,
                    maxTokens: maxTokens
                );

                TokenCounter.LogTokenBreakdown(breakdown, request.UserQuery);

                if (!TokenCounter.IsWithinLimit(breakdown.EstimatedTotalTokens, MAX_TOKENS_LIMIT))
                {
                    _logger.LogWarning(
                        "⚠️ Token limit exceeded! Estimated: {EstimatedTokens}, Max: {MaxTokens} for query: {Query}",
                        breakdown.EstimatedTotalTokens, MAX_TOKENS_LIMIT, request.UserQuery);
                }
                else if (breakdown.EstimatedTotalTokens > WARNING_TOKEN_THRESHOLD)
                {
                    _logger.LogWarning(
                        "⚠️ High token usage: {EstimatedTokens} tokens for query: {Query}",
                        breakdown.EstimatedTotalTokens, request.UserQuery);
                }

                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    messages.Add(new SystemChatMessage(systemPrompt));
                }

                if (request.ConversationHistory?.Any() == true)
                {
                    foreach (var msg in request.ConversationHistory.TakeLast(5))
                    {
                        if (string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                            messages.Add(new UserChatMessage(msg.Content));
                        else
                            messages.Add(new AssistantChatMessage(msg.Content));
                    }
                }

                messages.Add(new UserChatMessage(request.UserQuery));

                var completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
                var responseText = completion.Value.Content.Count > 0 ? completion.Value.Content[0].Text : string.Empty;

                var result = new AIResponseResult
                {
                    Response = responseText,
                    Answer = responseText,
                    PromptTokens = completion.Value.Usage?.InputTokenCount ?? 0,
                    CompletionTokens = completion.Value.Usage?.OutputTokenCount ?? 0,
                    TokenCount = completion.Value.Usage?.TotalTokenCount ?? 0,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    Query = request.Query ?? request.UserQuery,
                    IsSuccess = true,
                    FromCache = false,
                    CacheLevel = "Generated"
                };

                _logger.LogInformation(
                    "📊 Actual token usage: Prompt={PromptTokens}, Completion={CompletionTokens}, Total={TotalTokens}",
                    result.PromptTokens, result.CompletionTokens, result.TokenCount);

                var tokensSaved = breakdown.EstimatedTotalTokens - result.TokenCount;
                if (tokensSaved > 10)
                {
                    _logger.LogInformation(
                        "✅ Token savings: {TokensSaved} tokens saved! (Estimated: {Estimated}, Actual: {Actual})",
                        tokensSaved, breakdown.EstimatedTotalTokens, result.TokenCount);
                }

                return result;
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

                    await Task.Delay(20, cancellationToken);
                }

                yield return new AIStreamChunk { IsComplete = true };
                yield break;
            }

            // ✅ Detect intent for streaming as well
            var intentType = IntentHelper.DetectIntent(request.UserQuery);
            request.IntentType = intentType;

            if ((intentType == "confirmation" || intentType == "followup")
                && !string.IsNullOrEmpty(request.LastBotQuestion))
            {
                var contextAwareQuery = IntentHelper.BuildContextAwareQuery(
                    request.UserQuery,
                    request.LastBotQuestion,
                    intentType
                );
                request.Query = request.UserQuery;
                request.UserQuery = contextAwareQuery;
                request.IsFollowUp = true;
            }

            var messages = new List<OpenAI.Chat.ChatMessage>();

            var systemPrompt = BuildSystemPrompt(request);

            var maxTokens = request.MaxTokens;
            var history = request.ConversationHistory?.ToList() ?? new List<ConversationMessage>();

            var breakdown = TokenCounter.GetTokenBreakdown(
                systemPrompt: systemPrompt,
                query: request.UserQuery,
                history: history,
                moduleData: request.ModuleData,
                maxTokens: maxTokens
            );

            TokenCounter.LogTokenBreakdown(breakdown, request.UserQuery);

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new SystemChatMessage(systemPrompt));
            }

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

        /// <summary>
        /// Builds system prompt with enhanced context handling
        /// </summary>
        private string BuildSystemPrompt(AIResponseRequest request)
        {
            var systemPrompt = request.SystemPrompt ?? _options.Value.SystemPrompt ??
                "You are a helpful assistant. Maintain conversation context and answer based on the provided information.";

            if (request.ModuleData == null || request.ModuleData.Count == 0)
            {
                return systemPrompt;
            }

            var contextBuilder = new StringBuilder();

            // ✅ 1. INTENT INFORMATION
            if (!string.IsNullOrEmpty(request.IntentType))
            {
                contextBuilder.AppendLine("=== USER INTENT ===");
                contextBuilder.AppendLine($"Detected Intent: {request.IntentType}");
                contextBuilder.AppendLine($"Description: {IntentHelper.GetIntentDescription(request.IntentType)}");
                contextBuilder.AppendLine();
            }

            // ✅ 2. LAST BOT QUESTION (For follow-ups)
            if (!string.IsNullOrEmpty(request.LastBotQuestion))
            {
                contextBuilder.AppendLine("=== LAST QUESTION ASKED BY ASSISTANT ===");
                contextBuilder.AppendLine($"The user is responding to: {request.LastBotQuestion}");
                contextBuilder.AppendLine();
            }

            // ✅ 3. RECENT CONVERSATION CONTEXT
            if (!string.IsNullOrEmpty(request.RecentUserMessages))
            {
                contextBuilder.AppendLine("=== RECENT CONVERSATION CONTEXT ===");
                contextBuilder.AppendLine(request.RecentUserMessages);
                contextBuilder.AppendLine();
            }

            // ✅ 4. DYNAMIC DATA (Highest Priority)
            if (request.ModuleData.TryGetValue("DynamicData", out var dynamicData) && !string.IsNullOrEmpty(dynamicData))
            {
                contextBuilder.AppendLine("=== DYNAMIC DATA (HIGHEST PRIORITY) ===");
                var truncated = dynamicData.Length > 300 ? dynamicData[..300] + "..." : dynamicData;
                contextBuilder.AppendLine($"  {truncated}");
                contextBuilder.AppendLine();
            }

            // ✅ 5. SEARCH RESULTS (Top 3 only)
            if (request.ModuleData.TryGetValue("SearchResults", out var searchResults) && !string.IsNullOrEmpty(searchResults))
            {
                contextBuilder.AppendLine("=== SEARCH RESULTS (Top 3) ===");
                var results = ExtractTopSearchResults(searchResults, 3);
                foreach (var result in results)
                {
                    var truncated = result.Length > 300 ? result[..300] + "..." : result;
                    contextBuilder.AppendLine($"  • {truncated}");
                }
                contextBuilder.AppendLine();
            }

            // ✅ 6. TOP RESULT
            if (request.ModuleData.TryGetValue("TopResult", out var topResult) && !string.IsNullOrEmpty(topResult))
            {
                contextBuilder.AppendLine("=== BEST MATCH ===");
                var cleaned = CleanResultText(topResult);
                var truncated = cleaned.Length > 300 ? cleaned[..300] + "..." : cleaned;
                contextBuilder.AppendLine($"  {truncated}");
                contextBuilder.AppendLine();
            }

            // ✅ 7. KNOWLEDGE BASE
            if (request.ModuleData.TryGetValue("KnowledgeBase", out var knowledge) && !string.IsNullOrEmpty(knowledge))
            {
                contextBuilder.AppendLine("=== KNOWLEDGE BASE ===");
                var truncated = knowledge.Length > 300 ? knowledge[..300] + "..." : knowledge;
                contextBuilder.AppendLine($"  {truncated}");
                contextBuilder.AppendLine();
            }

            // ✅ 8. USER CONTEXT
            var userContext = new List<string>();
            if (request.ModuleData.TryGetValue("UserId", out var userId) && !string.IsNullOrEmpty(userId))
                userContext.Add($"User ID: {userId}");
            if (request.ModuleData.TryGetValue("Division", out var division) && !string.IsNullOrEmpty(division))
                userContext.Add($"Division: {division}");
            if (request.ModuleData.TryGetValue("UtilityAccountNumber", out var account) && !string.IsNullOrEmpty(account))
                userContext.Add($"Account: {account}");
            if (request.ModuleData.TryGetValue("IsAuthenticated", out var auth) && !string.IsNullOrEmpty(auth))
                userContext.Add($"Authenticated: {auth}");

            if (userContext.Any())
            {
                contextBuilder.AppendLine("=== USER CONTEXT ===");
                foreach (var item in userContext)
                {
                    contextBuilder.AppendLine($"  {item}");
                }
                contextBuilder.AppendLine();
            }

            // ✅ 9. CONVERSATION SUMMARY
            if (request.ModuleData.TryGetValue("ConversationSummary", out var summary) && !string.IsNullOrEmpty(summary))
            {
                contextBuilder.AppendLine("=== CONVERSATION SUMMARY ===");
                var truncated = summary.Length > 200 ? summary[..200] + "..." : summary;
                contextBuilder.AppendLine($"  {truncated}");
                contextBuilder.AppendLine();
            }

            var fullPrompt = systemPrompt;

            if (contextBuilder.Length > 0)
            {
                fullPrompt += $"\n\n{contextBuilder.ToString()}";

                fullPrompt += @"
=== RESPONSE GUIDELINES ===
1. MAINTAIN CONTEXT: Consider what was discussed previously.
2. FOLLOW-UP HANDLING: If user says 'yes', 'no', 'tell me more', refer to 'Last Question Asked by Assistant'.
3. PRIORITY ORDER: Dynamic Data > Search Results > Knowledge Base.
4. KEEP RESPONSES CONCISE: Use only needed information.
5. If unsure what user is referring to, ask a clarifying question.
6. For location questions, use Division context.";
            }

            if (fullPrompt.Length > 3000)
            {
                fullPrompt = fullPrompt[..3000] + "...";
            }

            return fullPrompt;
        }

        private List<string> ExtractTopSearchResults(string searchResults, int maxResults)
        {
            var resultList = new List<string>();

            if (string.IsNullOrEmpty(searchResults))
                return resultList;

            var parts = searchResults.Split(new[] { "Result " }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts.Take(maxResults))
            {
                var cleanPart = part.Trim();
                var lines = cleanPart.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var summary = new StringBuilder();

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("### Question Variants") ||
                        trimmed.StartsWith("### Answer") ||
                        trimmed.StartsWith("Score:") ||
                        trimmed.StartsWith("Metadata:"))
                    {
                        continue;
                    }
                    if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > 10)
                    {
                        summary.AppendLine(trimmed);
                    }
                }

                if (summary.Length > 0)
                {
                    var summaryText = summary.ToString();
                    if (summaryText.Length > 500)
                    {
                        summaryText = summaryText.Substring(0, 500) + "...";
                    }
                    resultList.Add(summaryText.Trim());
                }
            }

            return resultList;
        }

        private string CleanResultText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var cleanText = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = cleanText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var filteredLines = lines
                .Where(line => !line.Trim().StartsWith("###") &&
                              !line.Trim().StartsWith("Metadata:") &&
                              !string.IsNullOrWhiteSpace(line))
                .Take(10)
                .Select(line => line.Trim());

            var result = string.Join(" ", filteredLines);

            if (result.Length > 300)
            {
                result = result.Substring(0, 300) + "...";
            }

            return result;
        }

        private string GenerateMockResponse(string query)
        {
            var lowerQuery = query.ToLowerInvariant();

            var responses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["what is artificial intelligence"] = "Artificial intelligence (AI) is the simulation of human intelligence in machines.",
                ["explain machine learning"] = "Machine learning is a subset of AI that enables systems to learn from data.",
                ["difference between ai and ml"] = "AI is the broader concept, ML is a subset that learns from data.",
                ["meaning of life"] = "The meaning of life is subjective and varies from person to person.",
                ["quantum computing"] = "Quantum computing harnesses quantum mechanics to process information.",
                ["capital of france"] = "The capital of France is Paris.",
                ["renewable energy"] = "Renewable energy comes from natural sources that are constantly replenished.",
                ["neural networks"] = "Neural networks are computing systems inspired by biological neural networks.",
                ["deep learning"] = "Deep learning uses neural networks with multiple layers to learn from data.",
                ["natural language processing"] = "NLP helps computers understand and manipulate human language."
            };

            foreach (var key in responses.Keys)
            {
                if (lowerQuery.Contains(key))
                {
                    return responses[key];
                }
            }

            return $"I understand you're asking about '{query}'. This is a mock response for testing.";
        }


        private void BuildConversationContext(AIResponseRequest request)
        {
            if (request.ConversationHistory == null || !request.ConversationHistory.Any())
                return;

            // ✅ Extract latest messages for context
            var latestMessages = request.ConversationHistory.TakeLast(5);

            var contextLines = new List<string>();
            var lastQuestion = string.Empty;
            var recentMessages = new List<string>();

            foreach (var msg in latestMessages)
            {
                var role = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase) ? "User" : "Assistant";
                var content = msg.Content?.Length > 150 ? msg.Content[..150] + "..." : msg.Content;

                // Track the last question from the bot
                if (!string.IsNullOrEmpty(msg.Content) &&
                    string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it's a question (contains "?" or starts with "would", "could", etc.)
                    if (msg.Content.Contains("?") ||
                        msg.Content.StartsWith("Would", StringComparison.OrdinalIgnoreCase) ||
                        msg.Content.StartsWith("Could", StringComparison.OrdinalIgnoreCase) ||
                        msg.Content.StartsWith("Do you", StringComparison.OrdinalIgnoreCase) ||
                        msg.Content.StartsWith("Can I", StringComparison.OrdinalIgnoreCase) ||
                        msg.Content.StartsWith("Would you", StringComparison.OrdinalIgnoreCase) ||
                        msg.Content.StartsWith("Did you", StringComparison.OrdinalIgnoreCase))
                    {
                        lastQuestion = content;
                    }
                }

                recentMessages.Add($"{role}: {content}");
            }

            // ✅ Set the context properties
            if (!string.IsNullOrEmpty(lastQuestion))
            {
                request.LastBotQuestion = lastQuestion;
            }

            if (recentMessages.Any())
            {
                request.RecentUserMessages = string.Join("\n", recentMessages);
            }

            // ✅ Detect intent from the latest user message
            if (request.ConversationHistory.Any())
            {
                var latestUserMessage = request.ConversationHistory
                    .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                    .LastOrDefault();

                if (latestUserMessage != null)
                {
                    var intentType = IntentHelper.DetectIntent(latestUserMessage.Content);
                    request.IntentType = intentType;

                    // ✅ Build context-aware query if it's a follow-up
                    if ((intentType == "confirmation" || intentType == "followup" || intentType == "negation")
                        && !string.IsNullOrEmpty(request.LastBotQuestion))
                    {
                        var contextAwareQuery = IntentHelper.BuildContextAwareQuery(
                            latestUserMessage.Content,
                            request.LastBotQuestion,
                            intentType
                        );

                        // Use context-aware query for processing
                        request.Query = latestUserMessage.Content;
                        request.UserQuery = contextAwareQuery;
                        request.IsFollowUp = true;
                    }
                }
            }

            _logger.LogDebug($"📝 Context built: LastQuestion='{lastQuestion}', RecentMessages={recentMessages.Count}, Intent={request.IntentType}");
        }
    }
}