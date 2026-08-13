// Core/Services/AIResponseService.cs (Using new OpenAI SDK)
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class AIResponseService : IAIResponseService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<AIResponseService> _logger;
        private readonly ChatClient _chatClient;
        private readonly AIResponseOptions _options;
        private static readonly Regex UrlRegex = new Regex(
            @"https?:\/\/[^\s]+|www\.[^\s]+|[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}(?:\/[^\s]*)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

            // Create the ChatClient
            _chatClient = new ChatClient(
                model: deploymentName,
                credential: new ApiKeyCredential(key),
                options: new OpenAIClientOptions
                {
                    Endpoint = new Uri(endpoint)
                });
        }

        public async Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request);

            try
            {
                // Build module context with dynamic data priority
                var moduleContext = BuildModuleContext(request.ModuleData);

                // Check for dynamic data in latest user query (Rule 7)
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

                // Create completion options
                var completionOptions = new ChatCompletionOptions
                {
                    Temperature = request.Temperature > 0 ? request.Temperature : _options.DefaultTemperature,
                    MaxOutputTokenCount = request.MaxTokens > 0 ? request.MaxTokens : _options.DefaultMaxTokens
                };

                // Get response from OpenAI
                var response = await _chatClient.CompleteChatAsync(
                    messages,
                    completionOptions,
                    cancellationToken);

                var rawResponse = response.Value.Content[0].Text.Trim();

                // Remove URLs from response if enabled (Rule 6)
                var cleanedResponse = request.RemoveUrlsFromResponse
                    ? RemoveUrlsFromResponse(rawResponse)
                    : rawResponse;

                var result = new AIResponseResult
                {
                    Response = cleanedResponse,
                    OriginalResponse = rawResponse,
                    ConversationSummary = request.ConversationSummary,
                    TokenCount = response.Value.Usage.TotalTokenCount,
                    UsedSummary = !string.IsNullOrWhiteSpace(request.ConversationSummary),
                    HadUrlsRemoved = request.RemoveUrlsFromResponse && rawResponse != cleanedResponse,
                    DynamicDataUsed = dynamicDataContext
                };

                _logger.LogInformation(
                    "Generated AI response for conversation {ConversationId}, " +
                    "used {TokenCount} tokens, summary: {UsedSummary}, " +
                    "dynamic data: {HasDynamicData}, URLs removed: {UrlsRemoved}",
                    request.ConversationId,
                    result.TokenCount,
                    result.UsedSummary,
                    !string.IsNullOrEmpty(dynamicDataContext),
                    result.HadUrlsRemoved);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response for conversation {ConversationId}",
                    request.ConversationId);
                throw;
            }
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

            // Dynamic data from latest query - HIGHEST PRIORITY (Rule 7)
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
                if (msg.Content.Equals("User", StringComparison.OrdinalIgnoreCase))
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
    }
}