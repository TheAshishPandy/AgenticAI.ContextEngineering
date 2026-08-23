// AgenticAI.ContextEngineering.Core/Models/SearchContext.cs
using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    /// <summary>
    /// Context information for search operations
    /// </summary>
    public class SearchContext
    {
        /// <summary>
        /// Recent user messages in the conversation
        /// </summary>
        public List<string> RecentUserMessages { get; set; } = new();

        /// <summary>
        /// Recent bot messages in the conversation
        /// </summary>
        public List<string> RecentBotMessages { get; set; } = new();

        /// <summary>
        /// Total length of the conversation
        /// </summary>
        public int ConversationLength { get; set; }

        /// <summary>
        /// Additional contextual data
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }

        /// <summary>
        /// Current conversation ID
        /// </summary>
        public string? ConversationId { get; set; }

        /// <summary>
        /// User ID
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Timestamp of the query
        /// </summary>
        public DateTime QueryTimestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the context is empty
        /// </summary>
        public bool IsEmpty => RecentUserMessages.Count == 0 && RecentBotMessages.Count == 0;
    }
}