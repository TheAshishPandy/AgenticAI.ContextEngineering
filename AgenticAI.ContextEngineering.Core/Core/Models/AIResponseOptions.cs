// Core/Models/AIResponseOptions.cs
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    public class AIResponseOptions
    {
        public float DefaultTemperature { get; set; } = 0.2f;
        public int DefaultMaxTokens { get; set; } = 500;
        public int SummaryThreshold { get; set; } = 15;
        public int MaxHistoryMessages { get; set; } = 10;
        public int SummaryMaxTokens { get; set; } = 200;
        public float SummaryTemperature { get; set; } = 0.2f;

        // New options for URL removal and dynamic data
        public bool RemoveUrlsByDefault { get; set; } = true;
        public bool PrioritizeDynamicDataByDefault { get; set; } = true;
        public List<string> DynamicDataKeywords { get; set; } = new()
        {
            "current", "latest", "recent", "today", "now", "new", "updated"
        };
    }
}