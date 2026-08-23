using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Core.Models
{
    public class LLMRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public int MaxTokens { get; set; } = 500;
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.9f;
        public List<string>? StopSequences { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }

    public class LLMResponse
    {
        public string Text { get; set; } = string.Empty;
        public double? Confidence { get; set; }
        public int TokenCount { get; set; }
        public int PromptTokens { get; set; }  // ✅ Added
        public int CompletionTokens { get; set; }  // ✅ Added
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
