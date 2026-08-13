// Core/Services/TokenEstimator.cs
using System;
using System.Collections.Generic;
using System.Text.Json;
using TiktokenSharp;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Services
{
    public class TokenEstimator : ITokenEstimator
    {
        private readonly TikToken _tokenizer;
        private readonly Dictionary<string, int> _cache = new();

        // ✅ Parameterless constructor with safe default
        public TokenEstimator() : this("cl100k_base")  // Use cl100k_base for GPT-4/GPT-3.5
        {
        }

        public TokenEstimator(string model = "cl100k_base")  // ✅ Changed from "gpt-4" to "cl100k_base"
        {
            try
            {
                // Try the requested model first
                _tokenizer = TikToken.EncodingForModel(model);
            }
            catch (Exception ex)
            {
                // Fallback to cl100k_base (most compatible)
                try
                {
                    _tokenizer = TikToken.EncodingForModel("cl100k_base");
                    Console.WriteLine($"⚠️ TokenEstimator: Falling back to cl100k_base encoding");
                }
                catch
                {
                    // Ultimate fallback: p50k_base
                    try
                    {
                        _tokenizer = TikToken.EncodingForModel("p50k_base");
                        Console.WriteLine($"⚠️ TokenEstimator: Falling back to p50k_base encoding");
                    }
                    catch
                    {
                        throw new InvalidOperationException(
                            $"Failed to initialize tokenizer. Tried models: {model}, cl100k_base, p50k_base. " +
                            $"Error: {ex.Message}");
                    }
                }
            }
        }

        public int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            if (_cache.TryGetValue(text, out var cached))
                return cached;

            try
            {
                var tokens = _tokenizer.Encode(text).Count;
                _cache[text] = tokens;
                return tokens;
            }
            catch
            {
                // Fallback: rough estimate (4 chars ≈ 1 token)
                return text.Length / 4;
            }
        }

        public int EstimateTokens(object obj)
        {
            if (obj == null) return 0;
            try
            {
                var json = JsonSerializer.Serialize(obj);
                return EstimateTokens(json);
            }
            catch
            {
                return obj.ToString()?.Length / 4 ?? 0;
            }
        }

        public int EstimateTokens(List<ConversationMessage> messages)
        {
            if (messages == null || messages.Count == 0) return 0;
            int total = 0;
            foreach (var message in messages)
            {
                total += EstimateTokens(message.Content);
            }
            return total;
        }

        public int EstimateTokens(CompressableContext context)
        {
            if (context == null) return 0;

            int total = 0;
            total += EstimateTokens(context.SystemPrompt);
            total += EstimateTokens(context.Messages);
            total += EstimateTokens(context.ToolResults);
            total += EstimateTokens(JsonSerializer.Serialize(context.Metadata));

            return total;
        }

        private int EstimateTokens(List<ToolResult> toolResults)
        {
            if (toolResults == null || toolResults.Count == 0) return 0;
            int total = 0;
            foreach (var result in toolResults)
            {
                total += EstimateTokens(result.Output);
            }
            return total;
        }

        public Dictionary<string, int> GetTokenDistribution(Dictionary<string, object> context)
        {
            var distribution = new Dictionary<string, int>();
            foreach (var kvp in context)
            {
                distribution[kvp.Key] = EstimateTokens(kvp.Value);
            }
            return distribution;
        }
    }
}