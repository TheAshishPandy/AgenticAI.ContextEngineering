// Core/Services/SecretRedactor.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SmartChatBot.ContextEngineering.Core.Interfaces;
using SmartChatBot.ContextEngineering.Core.Models;

namespace SmartChatBot.ContextEngineering.Core.Strategies
{
    /// <summary>
    /// Redacts sensitive information from context (PII, API keys, etc.)
    /// </summary>
    public class SecretRedactor : ISecretRedactor
    {
        private readonly List<(Regex Pattern, string Replacement)> _rules;

        public SecretRedactor()
        {
            _rules = new List<(Regex, string)>
            {
                // Email addresses
                (new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b"), "[EMAIL_REDACTED]"),
                
                // Phone numbers (US format)
                (new Regex(@"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b"), "[PHONE_REDACTED]"),
                
                // API Keys
                (new Regex(@"(?i)(api[_-]?key|apikey|token|secret)\s*[:=]\s*['""]?[A-Za-z0-9\-_]{20,}['""]?"), "[API_KEY_REDACTED]"),
                
                // SSN
                (new Regex(@"\b\d{3}-\d{2}-\d{4}\b"), "[SSN_REDACTED]"),
                
                // Credit Card
                (new Regex(@"\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b"), "[CC_REDACTED]"),
                
                // JWT Tokens
                (new Regex(@"eyJ[a-zA-Z0-9\-_]+\.[a-zA-Z0-9\-_]+\.[a-zA-Z0-9\-_]+"), "[JWT_REDACTED]"),
                
                // IP Addresses
                (new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b"), "[IP_REDACTED]")
            };
        }

        public async Task<CompressableContext> RedactAsync(
            CompressableContext context,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var redacted = new CompressableContext
            {
                ConversationId = context.ConversationId,
                UserId = context.UserId,
                SystemPrompt = RedactText(context.SystemPrompt),
                Messages = new List<ChatMessage>(),
                ToolResults = new List<ToolResult>(),
                Metadata = new Dictionary<string, object>(context.Metadata),
                Timestamp = context.Timestamp
            };

            // Redact messages
            if (context.Messages != null)
            {
                foreach (var msg in context.Messages)
                {
                    redacted.Messages.Add(new ChatMessage
                    {
                        Role = msg.Role,
                        Content = RedactText(msg.Content),
                        Timestamp = msg.Timestamp
                    });
                }
            }

            // Redact tool results
            if (context.ToolResults != null)
            {
                foreach (var result in context.ToolResults)
                {
                    redacted.ToolResults.Add(new ToolResult
                    {
                        ToolName = result.ToolName,
                        Output = RedactText(result.Output),
                        IsError = result.IsError,
                        Timestamp = result.Timestamp
                    });
                }
            }

            return redacted;
        }

        private string RedactText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            foreach (var (pattern, replacement) in _rules)
            {
                text = pattern.Replace(text, replacement);
            }

            return text;
        }
    }
}