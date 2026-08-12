using System;
using System.Collections.Generic;

namespace SmartChatBot.ContextEngineering.Core.Models
{
    /// <summary>
    /// Anchor - protected context element
    /// </summary>
    public class Anchor
    {
        public AnchorType Type { get; set; }
        public int Index { get; set; }
        public string Reason { get; set; } = string.Empty;
        public double Importance { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Anchor type
    /// </summary>
    public enum AnchorType
    {
        Message,
        ToolResult,
        SystemPrompt,
        UserQuery,
        ImportantFact
    }
}