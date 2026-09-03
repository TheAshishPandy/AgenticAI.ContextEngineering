// AgenticAI.ContextEngineering.Core/Helpers/IntentHelper.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace AgenticAI.ContextEngineering.Core.Helpers
{
    public static class IntentHelper
    {
        private static readonly HashSet<string> _confirmationIntents = new(StringComparer.OrdinalIgnoreCase)
        {
            "yes", "yeah", "yep", "sure", "ok", "okay", "please", "go ahead",
            "tell me", "proceed", "correct", "right", "that's right", "exactly",
            "true", "absolutely", "definitely", "of course", "sounds good"
        };

        private static readonly HashSet<string> _negationIntents = new(StringComparer.OrdinalIgnoreCase)
        {
            "no", "nope", "not", "never", "no thanks", "not really", "wrong",
            "incorrect", "false", "nah", "negative"
        };

        private static readonly HashSet<string> _followUpIntents = new(StringComparer.OrdinalIgnoreCase)
        {
            "tell me more", "more", "elaborate", "explain", "what about", "how about",
            "and", "also", "additionally", "furthermore", "moreover", "continue",
            "go on", "next", "then", "after that", "what else"
        };

        private static readonly HashSet<string> _locationIntents = new(StringComparer.OrdinalIgnoreCase)
        {
            "near me", "location", "address", "where", "available", "close", "nearby",
            "in", "at", "around", "here", "there", "distance", "far", "nearest"
        };

        public static bool IsConfirmation(string query)
        {
            if (string.IsNullOrEmpty(query))
                return false;

            var lower = query.Trim().ToLowerInvariant();
            return _confirmationIntents.Any(i => lower.Contains(i)) || (lower.Length < 5 && !lower.Contains("?"));
        }

        public static bool IsNegation(string query)
        {
            if (string.IsNullOrEmpty(query))
                return false;

            var lower = query.Trim().ToLowerInvariant();
            return _negationIntents.Any(i => lower.Contains(i));
        }

        public static bool IsFollowUp(string query)
        {
            if (string.IsNullOrEmpty(query))
                return false;

            var lower = query.Trim().ToLowerInvariant();
            return _followUpIntents.Any(i => lower.Contains(i));
        }

        public static bool IsLocationQuery(string query)
        {
            if (string.IsNullOrEmpty(query))
                return false;

            var lower = query.Trim().ToLowerInvariant();
            return _locationIntents.Any(k => lower.Contains(k));
        }

        public static string DetectIntent(string query)
        {
            if (string.IsNullOrEmpty(query))
                return "unknown";

            if (IsConfirmation(query))
                return "confirmation";

            if (IsNegation(query))
                return "negation";

            if (IsFollowUp(query))
                return "followup";

            if (IsLocationQuery(query))
                return "location";

            return "question";
        }

        public static string BuildContextAwareQuery(string query, string lastBotQuestion, string intentType)
        {
            if (string.IsNullOrEmpty(lastBotQuestion))
                return query;

            return intentType switch
            {
                "confirmation" => $"User said '{query}' in response to the previous question: '{lastBotQuestion}'. The user is confirming or agreeing.",
                "negation" => $"User said '{query}' in response to the previous question: '{lastBotQuestion}'. The user is disagreeing or refusing.",
                "followup" => $"User said '{query}' in response to the previous question: '{lastBotQuestion}'. The user wants more information.",
                _ => query
            };
        }

        public static string GetIntentDescription(string intentType)
        {
            return intentType switch
            {
                "confirmation" => "The user is confirming or agreeing to the previous statement/question.",
                "negation" => "The user is disagreeing or refusing the previous statement/question.",
                "followup" => "The user wants more information about the previous topic.",
                "location" => "The user is asking about a location or place.",
                "question" => "The user is asking a new question.",
                "unknown" => "The intent could not be determined.",
                _ => "Unknown intent type."
            };
        }
    }
}