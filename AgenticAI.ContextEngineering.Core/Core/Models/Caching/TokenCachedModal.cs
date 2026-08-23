using AgenticAI.ContextEngineering.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Core.Models.Caching
{
   
    /// <summary>
    /// Cached Completion
    /// </summary>


    /// <summary>
    /// Cached Conversation
    /// </summary>
    public class CachedConversation
    {
        public string ConversationId { get; set; } = string.Empty;
        public List<CachedMessage> Messages { get; set; } = new();
        public int TotalTokens { get; set; }
        public DateTime LastUpdated { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class CachedMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TokenStats
    {
        public long TotalTokensCached { get; set; }
        public long TotalTokensSaved { get; set; }
        public long TotalPromptsCached { get; set; }
        public long TotalCompletionsCached { get; set; }
        public long TotalEmbeddingsCached { get; set; }
        public double CacheHitRate { get; set; }
        public double CostSaved { get; set; }
        public DateTime StatsUpdated { get; set; } = DateTime.UtcNow;
        public Dictionary<string, long> TokenSavingsByType { get; set; } = new();
    }


    // Cache Models
    public class CachedPrompt
    {
        public string PromptHash { get; set; } = string.Empty;
        public string PromptText { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime LastUsed { get; set; }
        public int UseCount { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class CachedCompletion
    {
        public string CompletionHash { get; set; } = string.Empty;
        public string PromptHash { get; set; } = string.Empty;
        public string CompletionText { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime LastUsed { get; set; }
        public int UseCount { get; set; }
        public double Confidence { get; set; }
    }

    public class CachedEmbedding
    {
        public string TextHash { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public int Dimension { get; set; }
        public DateTime CachedAt { get; set; }
        public int UseCount { get; set; }
    }


}
