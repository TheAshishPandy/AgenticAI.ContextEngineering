using System;
using System.Collections.Generic;

namespace SmartChatBot.ContextEngineering.Core.Models
{
    /// <summary>
    /// Memory entry
    /// </summary>
    public class MemoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ConversationId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public MemoryType Type { get; set; }
        public double ImportanceScore { get; set; } = 0.5;
        public double ConfidenceScore { get; set; } = 0.5;
    }

    /// <summary>
    /// Memory type
    /// </summary>
    public enum MemoryType
    {
        Fact,
        Preference,
        Decision,
        Intent,
        Context,
        Entity
    }
}