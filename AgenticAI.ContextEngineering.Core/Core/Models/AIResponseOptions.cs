// Core/Models/AIResponseOptions.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class AIResponseOptions
    {
        // Azure OpenAI Configuration
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = "gpt-4";
        public string EmbeddingDeploymentName { get; set; } = "text-embedding-ada-002";
        public string ApiVersion { get; set; } = "2024-02-15-preview";

        // Response Settings
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 500;
        public float NucleusSamplingFactor { get; set; } = 0.95f;
        public int? FrequencyPenalty { get; set; }
        public int? PresencePenalty { get; set; }

        // Prompt Settings
        public string SystemPrompt { get; set; } = "You are a helpful assistant.";
        public string DefaultSystemPrompt { get; set; } = "You are a helpful assistant.";
        public int SummaryThreshold { get; set; } = 15;
        public int MaxHistoryMessages { get; set; } = 20;
        public bool RemoveUrlsFromResponse { get; set; } = true;
        public bool PrioritizeDynamicData { get; set; } = true;

        // Cache Settings
        public bool UseCache { get; set; } = true;
        public bool EnableTokenCaching { get; set; } = true;
        public int CacheExpirationHours { get; set; } = 24;
        public bool StreamingEnabled { get; set; } = false;

        // Model
        public string Model { get; set; } = "gpt-4";
    }

    // ✅ Keep these for JSON deserialization compatibility
    public class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; } = new();

        [JsonPropertyName("usage")]
        public Usage Usage { get; set; } = new();
    }

    public class Choice
    {
        [JsonPropertyName("message")]
        public Message Message { get; set; } = new();

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; } = string.Empty;

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }

    public class Message
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("refusal")]
        public string Refusal { get; set; } = string.Empty;
    }

    public class Usage
    {
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
    }
}