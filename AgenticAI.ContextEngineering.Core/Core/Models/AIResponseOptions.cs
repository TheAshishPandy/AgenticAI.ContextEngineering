// Core/Models/AIResponseOptions.cs
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class AIResponseOptions
    {
        public string AzureOpenAIEndpoint { get; set; } = string.Empty;
        public string AzureOpenAIKey { get; set; } = string.Empty;
        public string AzureOpenAIDeploymentName { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "2024-02-15-preview";
        public float DefaultTemperature { get; set; } = 0.2f;
        public int DefaultMaxTokens { get; set; } = 500;
        public int SummaryThreshold { get; set; } = 15;
        public int MaxHistoryMessages { get; set; } = 20;
        public bool RemoveUrlsFromResponse { get; set; } = true;
        public bool PrioritizeDynamicData { get; set; } = true;
        public string DefaultSystemPrompt { get; set; } = "You are a helpful assistant.";
    }
}