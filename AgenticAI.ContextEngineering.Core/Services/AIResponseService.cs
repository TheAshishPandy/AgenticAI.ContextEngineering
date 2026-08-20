// Core/Services/AIResponseService.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class AIResponseService : IAIResponseService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<AIResponseService> _logger;
        private readonly AIResponseOptions _options;
        private static readonly Regex UrlRegex = new Regex(
            @"https?:\/\/[^\s]+|www\.[^\s]+|[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}(?:\/[^\s]*)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public AIResponseService(
            IConfiguration configuration,
            ILogger<AIResponseService> logger,
            IOptions<AIResponseOptions> options)
        {
            _config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new AIResponseOptions();

            var endpoint = _config["AzureOpenAIEndpoint"] ?? throw new InvalidOperationException("AzureOpenAIEndpoint not configured");
            var key = _config["AzureOpenAIKey"] ?? throw new InvalidOperationException("AzureOpenAIKey not configured");
            var deploymentName = _config["AzureOpenAIDeploymentName"] ?? throw new InvalidOperationException("AzureOpenAIDeploymentName not configured");
            var apiVersion = _options.ApiVersion ?? "2024-02-15-preview";

            _logger.LogInformation($"Initializing AIResponseService with Endpoint: {endpoint}, Deployment: {deploymentName}, API Version: {apiVersion}");

            // Build the exact URL that works in Postman
            var baseUrl = endpoint.TrimEnd('/');
            _baseUrl = $"{baseUrl}/openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}";
            _apiKey = key;

            _logger.LogInformation($"Using URL: {_baseUrl}");

            // Create HttpClient with headers
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request);

            try
            {
                _logger.LogDebug("Generating AI response for conversation {ConversationId}, Division: {Division}",
                    request.ConversationId, request.Division);

                // Build module context with dynamic data priority
                var moduleContext = BuildModuleContext(request.ModuleData);

                // Check for dynamic data in latest user query
                var dynamicDataContext = ExtractDynamicDataFromQuery(
                    request.UserQuery,
                    request.ModuleData,
                    request.DynamicDataKeywords);

                // Prepare messages
                var messages = CreateMessages(
                    request,
                    moduleContext,
                    dynamicDataContext,
                    request.ConversationSummary,
                    request.ConversationHistory);

                // Build request body
                var requestBody = new
                {
                    messages = messages.Select(m => new
                    {
                        role = GetRoleName(m),
                        content = GetMessageContent(m)
                    }).ToList(),
                    max_tokens = request.MaxTokens > 0 ? request.MaxTokens : _options.DefaultMaxTokens,
                    temperature = request.Temperature > 0 ? request.Temperature : _options.DefaultTemperature
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Log the request for debugging
                _logger.LogDebug($"Calling Azure OpenAI at: {_baseUrl}");

                // Send request
                var response = await _httpClient.PostAsync(_baseUrl, content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"HTTP {response.StatusCode}: {responseContent}");
                    throw new Exception($"Azure OpenAI API error: {response.StatusCode} - {responseContent}");
                }

                // ✅ Parse response with correct model
                var result = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null || result.Choices == null || !result.Choices.Any())
                {
                    _logger.LogError("No choices returned from OpenAI");
                    throw new Exception("No response from OpenAI");
                }

                var rawResponse = result.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;

                // Remove URLs from response if enabled
                var cleanedResponse = request.RemoveUrlsFromResponse
                    ? RemoveUrlsFromResponse(rawResponse)
                    : rawResponse;

                var tokenDetails = new TokenDetails
                {
                    TotalTokens = result.Usage?.TotalTokens ?? 0,
                    PromptTokens = result.Usage?.PromptTokens ?? 0,
                    CompletionTokens = result.Usage?.CompletionTokens ?? 0
                };

                var aiResult = new AIResponseResult
                {
                    Response = cleanedResponse,
                    OriginalResponse = rawResponse,
                    ConversationSummary = request.ConversationSummary,
                    TokenCount = result.Usage?.TotalTokens ?? 0,
                    PromptTokens = result.Usage?.PromptTokens ?? 0,
                    CompletionTokens = result.Usage?.CompletionTokens ?? 0,
                    TokenDetails = tokenDetails,
                    UsedSummary = !string.IsNullOrWhiteSpace(request.ConversationSummary),
                    HadUrlsRemoved = request.RemoveUrlsFromResponse && rawResponse != cleanedResponse,
                    DynamicDataUsed = dynamicDataContext
                };

                _logger.LogInformation(
                    "Generated AI response for conversation {ConversationId}, TotalTokens: {TotalTokens}",
                    request.ConversationId, aiResult.TokenCount);

                return aiResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response for conversation {ConversationId}",
                    request.ConversationId);
                throw;
            }
        }

        private string GetRoleName(ChatMessage message)
        {
            return message switch
            {
                SystemChatMessage => "system",
                UserChatMessage => "user",
                AssistantChatMessage => "assistant",
                _ => "user"
            };
        }

        private string GetMessageContent(ChatMessage message)
        {
            // Get the content from the message
            var content = message.Content.FirstOrDefault();
            return content?.Text ?? string.Empty;
        }

        private void ValidateRequest(AIResponseRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ConversationId))
                throw new ArgumentException("ConversationId is required", nameof(request));

            if (string.IsNullOrWhiteSpace(request.Division))
                throw new ArgumentException("Division is required", nameof(request));

            if (string.IsNullOrWhiteSpace(request.UserQuery))
                throw new ArgumentException("UserQuery is required", nameof(request));
        }

        private string BuildModuleContext(Dictionary<string, string> moduleData)
        {
            if (moduleData == null || !moduleData.Any())
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var item in moduleData)
            {
                sb.AppendLine($"{item.Key}: {item.Value}");
            }
            return sb.ToString();
        }

        private string ExtractDynamicDataFromQuery(
            string userQuery,
            Dictionary<string, string> moduleData,
            List<string> dynamicKeywords)
        {
            if (string.IsNullOrWhiteSpace(userQuery) || moduleData == null || !moduleData.Any())
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("DYNAMIC DATA FROM LATEST QUERY:");

            var foundData = false;

            dynamicKeywords ??= new List<string>();

            foreach (var keyword in dynamicKeywords)
            {
                if (userQuery.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var item in moduleData)
                    {
                        if (item.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                            userQuery.Contains(item.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            sb.AppendLine($"  {item.Key}: {item.Value}");
                            foundData = true;
                        }
                    }
                }
            }

            foreach (var item in moduleData)
            {
                if (userQuery.Contains(item.Key, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"  {item.Key}: {item.Value}");
                    foundData = true;
                }
            }

            return foundData ? sb.ToString() : string.Empty;
        }

        private string RemoveUrlsFromResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return response;

            var cleaned = UrlRegex.Replace(response, string.Empty);
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            cleaned = Regex.Replace(cleaned, @"\s+\.", ".");
            cleaned = Regex.Replace(cleaned, @"\s+,", ",");
            cleaned = cleaned.Trim();
            cleaned = Regex.Replace(cleaned, @"\[.*?\]\(.*?\)", string.Empty);
            cleaned = Regex.Replace(cleaned, @"<.*?>", string.Empty);

            return cleaned;
        }

        private List<ChatMessage> CreateMessages(
            AIResponseRequest request,
            string moduleContext,
            string dynamicDataContext,
            string conversationSummary,
            List<ConversationMessage> conversationHistory)
        {
            var messages = new List<ChatMessage>();

            // System prompt
            messages.Add(new SystemChatMessage(GetSystemPrompt()));

            // Customer data context
            if (!string.IsNullOrWhiteSpace(moduleContext))
            {
                messages.Add(new UserChatMessage($"CURRENT CUSTOMER DATA:\n{moduleContext}"));
            }

            // Dynamic data from latest query - HIGHEST PRIORITY
            if (!string.IsNullOrWhiteSpace(dynamicDataContext) && request.PrioritizeDynamicData)
            {
                messages.Add(new UserChatMessage(
                    $"⚠️ HIGH PRIORITY - LATEST QUERY DYNAMIC DATA:\n{dynamicDataContext}\n" +
                    "Use this data as the primary source for your response."));
            }

            // Conversation summary (lower priority)
            if (!string.IsNullOrWhiteSpace(conversationSummary))
            {
                messages.Add(new UserChatMessage(
                    $"CONVERSATION SUMMARY (For context only):\n{conversationSummary}"));
            }

            // Recent conversation history
            var recentMessages = GetRecentMessages(conversationHistory);
            foreach (var msg in recentMessages)
            {
                if (msg.Role.Equals("User", StringComparison.OrdinalIgnoreCase))
                {
                    messages.Add(new UserChatMessage(msg.Content));
                }
                else
                {
                    messages.Add(new AssistantChatMessage(msg.Content));
                }
            }

            // Current user query
            var enhancedQuery = request.PrioritizeDynamicData
                ? $"[LATEST QUERY - PRIORITY OVERRIDE]\n{request.UserQuery}"
                : request.UserQuery;

            messages.Add(new UserChatMessage(enhancedQuery));

            return messages;
        }

        private string GetSystemPrompt()
        {
            return @"You are an intelligent utility assistant.

            CRITICAL RULES (in priority order):
            1. 🚨 HIGHEST PRIORITY: Use DYNAMIC DATA from the latest user query when present.
            2. Use CURRENT CUSTOMER DATA for general information.
            3. Use CONVERSATION HISTORY only for context and follow-up questions.
            4. Use CONVERSATION SUMMARY only for background context.
            
            ADDITIONAL RULES:
            5. Never invent or fabricate values.
            6. If information is unavailable, clearly state you don't know.
            7. ❌ ABSOLUTELY NO URLs OR LINKS in your response.
            8. Keep responses concise and focused.
            9. When dynamic data is provided, prioritize it over all other data sources.
            10. Always respond based on the most recent user query context.";
        }

        private List<ConversationMessage> GetRecentMessages(List<ConversationMessage> history)
        {
            if (history == null || !history.Any())
                return new List<ConversationMessage>();

            int takeCount = history.Count > _options.SummaryThreshold
                ? Math.Min(_options.MaxHistoryMessages / 2, history.Count)
                : Math.Min(_options.MaxHistoryMessages, history.Count);

            return history
                .OrderByDescending(x => x.Timestamp)
                .Take(takeCount)
                .OrderBy(x => x.Timestamp)
                .ToList();
        }

        // ✅ Fixed Response models with proper JSON property names
        private class OpenAIResponse
        {
            [JsonPropertyName("choices")]
            public List<Choice> Choices { get; set; } = new();

            [JsonPropertyName("usage")]
            public Usage Usage { get; set; } = new();
        }

        private class Choice
        {
            [JsonPropertyName("message")]
            public Message Message { get; set; } = new();

            [JsonPropertyName("finish_reason")]
            public string FinishReason { get; set; } = string.Empty;

            [JsonPropertyName("index")]
            public int Index { get; set; }
        }

        private class Message
        {
            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;

            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("refusal")]
            public string Refusal { get; set; } = string.Empty;
        }

        private class Usage
        {
            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }

            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }
        }
    }
}