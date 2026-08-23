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
    /// <summary>
    /// Simple AI Response Service - Just Azure OpenAI, no caching
    /// </summary>
    public class AIResponseService : IAIResponseService
    {
        private readonly ChatClient? _chatClient;
        private readonly IOptions<AIResponseOptions> _options;
        private readonly ILogger<AIResponseService> _logger;
        private readonly bool _isConfigured;

        public AIResponseService(
            AzureOpenAIClient? openAIClient,
            IOptions<AIResponseOptions> options,
            ILogger<AIResponseService> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                if (openAIClient != null)
                {
                    var deploymentName = _options.Value.DeploymentName ?? "gpt-4";
                    _chatClient = openAIClient.GetChatClient(deploymentName);
                    _isConfigured = true;
                    _logger.LogInformation("✅ Azure OpenAI client initialized");
                }
                else
                {
                    _logger.LogWarning("⚠️ AzureOpenAIClient is null");
                    _isConfigured = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize Azure OpenAI client");
                _isConfigured = false;
            }
        }

        public async Task<AIResponseResult> GenerateResponseAsync1(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (!_isConfigured || _chatClient == null)
                {
                    return new AIResponseResult
                    {
                        Response = "⚠️ Azure OpenAI is not configured. Please check your appsettings.json.",
                        IsSuccess = false,
                        Query = request.UserQuery,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        Error = "Azure OpenAI not configured"
                    };
                }

                _logger.LogDebug("Generating response for: {Query}", request.UserQuery);

                // Build messages
                var messages = new List<OpenAI.Chat.ChatMessage>
                {
                    new SystemChatMessage(_options.Value.SystemPrompt ?? "You are a helpful assistant.")
                };

                // Add conversation history
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

                // Add user query
                messages.Add(new UserChatMessage(request.UserQuery));

                // Call Azure OpenAI
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
                    FromCache = false
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
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        public async IAsyncEnumerable<AIStreamChunk> GenerateStreamingResponseAsync(
            AIResponseRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_isConfigured || _chatClient == null)
            {
                yield return new AIStreamChunk
                {
                    Content = "⚠️ Azure OpenAI is not configured.",
                    IsComplete = true,
                    Error = "Azure OpenAI not configured"
                };
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
            return Task.FromResult("Simple AI Service - No token tracking");
        }


        // AgenticAI.ContextEngineering.Core/Services/AIResponseService.cs - Add this method

     

        public async Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (!_isConfigured || _chatClient == null)
                {
                    return new AIResponseResult
                    {
                        Response = "⚠️ Azure OpenAI is not configured. Please check your appsettings.json.",
                        IsSuccess = false,
                        Query = request.UserQuery,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        Error = "Azure OpenAI not configured"
                    };
                }

                _logger.LogDebug("Generating response for: {Query}", request.UserQuery);

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
    }
}