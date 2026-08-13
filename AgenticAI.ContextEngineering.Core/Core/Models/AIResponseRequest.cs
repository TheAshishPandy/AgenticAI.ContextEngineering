// Core/Models/AIResponseRequest.cs
using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class AIResponseRequest
    {
        // Core properties
        public string ConversationId { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string UserQuery { get; set; } = string.Empty;
        public Dictionary<string, string> ModuleData { get; set; } = new();

        // Conversation history - passed from SmartBot
        public List<ConversationMessage> ConversationHistory { get; set; } = new();
        public string ConversationSummary { get; set; } = string.Empty;

        // Configuration
        public float Temperature { get; set; } = 0.2f;
        public int MaxTokens { get; set; } = 500;
        public bool RemoveUrlsFromResponse { get; set; } = true;
        public bool PrioritizeDynamicData { get; set; } = true;
        public List<string> DynamicDataKeywords { get; set; } = new()
        {
            "current", "latest", "recent", "today", "now", "new", "updated"
        };
    }

    public class AIResponseResult
    {
        public string Response { get; set; } = string.Empty;
        public string OriginalResponse { get; set; } = string.Empty;
        public string ConversationSummary { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public bool UsedSummary { get; set; }
        public bool HadUrlsRemoved { get; set; }
        public string DynamicDataUsed { get; set; } = string.Empty;
    }
}