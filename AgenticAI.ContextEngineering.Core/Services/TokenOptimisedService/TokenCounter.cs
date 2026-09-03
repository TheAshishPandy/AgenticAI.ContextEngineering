// AgenticAI.ContextEngineering.Core/Services/TokenCounter.cs
using AgenticAI.ContextEngineering.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TiktokenSharp;

namespace AgenticAI.ContextEngineering.Core.Services
{
    /// <summary>
    /// Token counter for accurate token usage before API calls
    /// Uses TiktokenSharp for accurate GPT token counting
    /// </summary>
    public static class TokenCounter
    {
        private static TikToken _tokenizer;
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        // Fallback constants if TiktokenSharp fails
        private const int CHARS_PER_TOKEN = 4;
        private const double WORDS_PER_TOKEN = 0.75;

        /// <summary>
        /// Initialize the tokenizer for a specific model
        /// Call this once at application startup
        /// </summary>
        public static void Initialize(string modelName = "gpt-4")
        {
            if (_isInitialized)
                return;

            lock (_lock)
            {
                if (_isInitialized)
                    return;

                try
                {
                    // ✅ Correct method: EncodingForModel (not GetEncodingByModel)
                    _tokenizer = TikToken.EncodingForModel(modelName);
                    _isInitialized = true;
                    Console.WriteLine($"✅ Tokenizer initialized for model: {modelName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to initialize TiktokenSharp: {ex.Message}");
                    Console.WriteLine("   Using fallback approximate token counting method.");
                    _tokenizer = null;
                    _isInitialized = true;
                }
            }
        }

        /// <summary>
        /// Initialize the tokenizer with a specific encoding
        /// </summary>
        public static void InitializeWithEncoding(string encodingName = "cl100k_base")
        {
            if (_isInitialized)
                return;

            lock (_lock)
            {
                if (_isInitialized)
                    return;

                try
                {
                    // ✅ Correct method: GetEncoding
                    _tokenizer = TikToken.GetEncoding(encodingName);
                    _isInitialized = true;
                    Console.WriteLine($"✅ Tokenizer initialized with encoding: {encodingName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to initialize TiktokenSharp: {ex.Message}");
                    Console.WriteLine("   Using fallback approximate token counting method.");
                    _tokenizer = null;
                    _isInitialized = true;
                }
            }
        }

        /// <summary>
        /// Count tokens in a string using TiktokenSharp (accurate)
        /// </summary>
        public static int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Try using TiktokenSharp for accurate counting
            if (_tokenizer != null)
            {
                try
                {
                    // ✅ Correct method: Encode and count
                    var encoded = _tokenizer.Encode(text);
                    return encoded.Count;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ TiktokenSharp error: {ex.Message}");
                }
            }

            // Fallback: approximate counting
            return ApproximateCountTokens(text);
        }

        /// <summary>
        /// Count tokens in a list of messages
        /// </summary>
        public static int CountTokens(IEnumerable<ConversationMessage> messages)
        {
            if (messages == null || !messages.Any())
                return 0;

            return messages.Sum(m => CountTokens(m.Content));
        }

        /// <summary>
        /// Count tokens in a dictionary (module data)
        /// </summary>
        public static int CountTokens(Dictionary<string, string> moduleData)
        {
            if (moduleData == null || !moduleData.Any())
                return 0;

            var text = string.Join(" ", moduleData.Select(kv => $"{kv.Key}: {kv.Value}"));
            return CountTokens(text);
        }

        /// <summary>
        /// Get detailed token breakdown for a request
        /// </summary>
        public static TokenBreakdown GetTokenBreakdown(
            string systemPrompt,
            string query,
            List<ConversationMessage> history,
            Dictionary<string, string> moduleData = null,
            int maxTokens = 150)
        {
            var breakdown = new TokenBreakdown
            {
                SystemPromptTokens = CountTokens(systemPrompt ?? string.Empty),
                QueryTokens = CountTokens(query ?? string.Empty),
                HistoryTokens = CountTokens(history ?? new List<ConversationMessage>()),
                ModuleDataTokens = CountTokens(moduleData),
                MaxCompletionTokens = maxTokens
            };

            breakdown.TotalPromptTokens = breakdown.SystemPromptTokens
                + breakdown.QueryTokens
                + breakdown.HistoryTokens
                + breakdown.ModuleDataTokens;

            breakdown.EstimatedTotalTokens = breakdown.TotalPromptTokens + maxTokens;

            return breakdown;
        }

        /// <summary>
        /// Approximate token count (fallback method)
        /// </summary>
        private static int ApproximateCountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Method 1: Character-based estimation
            var charCount = text.Length;
            var tokenEstimate = charCount / CHARS_PER_TOKEN;

            // Method 2: Word-based estimation
            var wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var wordEstimate = (int)(wordCount / WORDS_PER_TOKEN);

            return Math.Max(1, (tokenEstimate + wordEstimate) / 2);
        }

        /// <summary>
        /// Check if token usage is within limits
        /// </summary>
        public static bool IsWithinLimit(int totalTokens, int maxLimit = 4000)
        {
            return totalTokens <= maxLimit;
        }

        /// <summary>
        /// Get optimization suggestions based on token breakdown
        /// </summary>
        public static List<string> GetOptimizationSuggestions(TokenBreakdown breakdown)
        {
            var suggestions = new List<string>();

            if (breakdown.SystemPromptTokens > 200)
            {
                suggestions.Add($"System prompt is {breakdown.SystemPromptTokens} tokens. Consider reducing to under 200 tokens.");
            }

            if (breakdown.QueryTokens > 100)
            {
                suggestions.Add($"Query is {breakdown.QueryTokens} tokens. Consider shortening the query.");
            }

            if (breakdown.HistoryTokens > 300)
            {
                suggestions.Add($"History is {breakdown.HistoryTokens} tokens. Consider limiting to last 3-5 messages.");
            }

            if (breakdown.ModuleDataTokens > 200)
            {
                suggestions.Add($"Module data is {breakdown.ModuleDataTokens} tokens. Consider truncating or reducing.");
            }

            if (breakdown.TotalPromptTokens > 800)
            {
                suggestions.Add($"Total prompt tokens is {breakdown.TotalPromptTokens}. Consider reducing prompt size.");
            }

            if (breakdown.EstimatedTotalTokens > 1500)
            {
                suggestions.Add($"Estimated total tokens is {breakdown.EstimatedTotalTokens}. Consider reducing max completion tokens.");
            }

            return suggestions;
        }

        /// <summary>
        /// Log token breakdown to console
        /// </summary>
        public static void LogTokenBreakdown(TokenBreakdown breakdown, string query)
        {
            Console.WriteLine($"\n🔢 TOKEN BREAKDOWN for: {query}");
            Console.WriteLine(new string('─', 55));
            Console.WriteLine($"   System Prompt:   {breakdown.SystemPromptTokens,6} tokens");
            Console.WriteLine($"   Query:           {breakdown.QueryTokens,6} tokens");
            Console.WriteLine($"   History:         {breakdown.HistoryTokens,6} tokens");
            Console.WriteLine($"   Module Data:     {breakdown.ModuleDataTokens,6} tokens");
            Console.WriteLine($"   ───────────────────────");
            Console.WriteLine($"   Total Prompt:    {breakdown.TotalPromptTokens,6} tokens");
            Console.WriteLine($"   Max Completion:  {breakdown.MaxCompletionTokens,6} tokens");
            Console.WriteLine($"   ───────────────────────");
            Console.WriteLine($"   Estimated Total: {breakdown.EstimatedTotalTokens,6} tokens");
            Console.WriteLine(new string('─', 55));

            if (breakdown.TotalPromptTokens > 800)
            {
                Console.WriteLine($"   ⚠️  WARNING: High prompt tokens ({breakdown.TotalPromptTokens})!");
                Console.WriteLine($"   💡 Consider reducing system prompt or history.");
            }

            if (breakdown.EstimatedTotalTokens > 1500)
            {
                Console.WriteLine($"   ⚠️  WARNING: High estimated total tokens ({breakdown.EstimatedTotalTokens})!");
                Console.WriteLine($"   💡 Consider reducing max completion tokens.");
            }

            // Show suggestions
            var suggestions = GetOptimizationSuggestions(breakdown);
            if (suggestions.Any())
            {
                Console.WriteLine($"\n   💡 Optimization Suggestions:");
                foreach (var suggestion in suggestions)
                {
                    Console.WriteLine($"      • {suggestion}");
                }
            }
        }
    }

    /// <summary>
    /// Token breakdown details
    /// </summary>
    public class TokenBreakdown
    {
        public int SystemPromptTokens { get; set; }
        public int QueryTokens { get; set; }
        public int HistoryTokens { get; set; }
        public int ModuleDataTokens { get; set; }
        public int TotalPromptTokens { get; set; }
        public int MaxCompletionTokens { get; set; }
        public int EstimatedTotalTokens { get; set; }

        public override string ToString()
        {
            return $"System: {SystemPromptTokens}, Query: {QueryTokens}, History: {HistoryTokens}, Module: {ModuleDataTokens}, Total: {TotalPromptTokens}, Completion: {MaxCompletionTokens}, Estimated: {EstimatedTotalTokens}";
        }
    }
}