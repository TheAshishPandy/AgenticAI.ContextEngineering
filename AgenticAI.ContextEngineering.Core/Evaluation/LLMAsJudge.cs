// Core/Evaluation/LLMAsJudge.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using ChatMessage = OpenAI.Chat.ChatMessage;

namespace AgenticAI.ContextEngineering.Core.Evaluation
{
    public class LLMAsJudge : ILLMAsJudge
    {
        private readonly ChatClient? _chatClient;
        private readonly ILogger<LLMAsJudge> _logger;
        private readonly bool _isEnabled;
        private readonly string _modelName;

        public LLMAsJudge(IConfiguration config, ILogger<LLMAsJudge> logger)
        {
            _logger = logger;
            _modelName = "wechat";  // Your deployment name

            // Get configuration
            var endpoint = config?["AzureOpenAI:Endpoint"];
            var key = config?["AzureOpenAI:Key"];
            var deploymentName = config?["AzureOpenAI:DeploymentName"] ?? "wechat";

            if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(key))
            {
                try
                {
                    var client = new AzureOpenAIClient(
                        new Uri(endpoint),
                        new Azure.AzureKeyCredential(key));
                    _chatClient = client.GetChatClient(deploymentName);
                    _isEnabled = true;
                    _logger.LogInformation($"LLMAsJudge initialized with model: {deploymentName}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initialize OpenAI client. LLM evaluation will be disabled.");
                    _isEnabled = false;
                }
            }
            else
            {
                _logger.LogWarning("Azure OpenAI configuration missing. LLM evaluation will be disabled.");
                _isEnabled = false;
            }
        }

        public async Task<EvaluationResult> EvaluateAsync(
            string userQuery,
            string response,
            List<EvaluationCriterion> criteria,
            CancellationToken cancellationToken = default)
        {
            if (!_isEnabled || _chatClient == null)
            {
                return new EvaluationResult
                {
                    Success = false,
                    Error = "LLM evaluation is disabled due to missing configuration"
                };
            }

            var prompt = BuildEvaluationPrompt(userQuery, response, criteria);

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(
                    "You are an expert evaluator of AI assistant responses. " +
                    "Evaluate the response based on the provided criteria. " +
                    "Return your evaluation in valid JSON format with the following structure: " +
                    "{ \"overall_score\": 0-10, \"criteria_scores\": { \"criterion_name\": score }, " +
                    "\"strengths\": [\"strength1\", \"strength2\"], \"weaknesses\": [\"weakness1\", \"weakness2\"], " +
                    "\"reasoning\": \"your reasoning here\" }"),
                OpenAI.Chat.ChatMessage.CreateUserMessage(prompt)
            };

            try
            {
                var result = await _chatClient.CompleteChatAsync(
                    messages,
                    new ChatCompletionOptions
                    {
                        Temperature = 0.2f,
                        MaxOutputTokenCount = 500
                    },
                    cancellationToken);
                 
                var content = result.Value.Content[0].Text;
                return ParseEvaluationResult(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Evaluation failed");
                return new EvaluationResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<ComparisonResult> CompareAsync(
            string userQuery,
            string responseA,
            string responseB,
            List<EvaluationCriterion> criteria,
            CancellationToken cancellationToken = default)
        {
            if (!_isEnabled || _chatClient == null)
            {
                return new ComparisonResult
                {
                    Success = false,
                    Error = "LLM evaluation is disabled due to missing configuration"
                };
            }

            var prompt = BuildComparisonPrompt(userQuery, responseA, responseB, criteria);

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(
                    "You are an expert evaluator comparing two AI responses. " +
                    "Analyze which response is better and explain why. " +
                    "Return your comparison in valid JSON format with: " +
                    "{ \"winner\": \"A\" or \"B\", \"scores\": { \"response_a\": score, \"response_b\": score }, " +
                    "\"reasoning\": \"your detailed reasoning here\" }"),
                ChatMessage.CreateUserMessage(prompt)
            };

            try
            {
                var result = await _chatClient.CompleteChatAsync(
                    messages,
                    new ChatCompletionOptions
                    {
                        Temperature = 0.2f,
                        MaxOutputTokenCount = 500
                    },
                    cancellationToken);

                var content = result.Value.Content[0].Text;
                return ParseComparisonResult(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Comparison failed");
                return new ComparisonResult { Success = false, Error = ex.Message };
            }
        }

        private string BuildEvaluationPrompt(string userQuery, string response, List<EvaluationCriterion> criteria)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"User Query: {userQuery}");
            sb.AppendLine($"Response to Evaluate: {response}");
            sb.AppendLine();
            sb.AppendLine("Evaluation Criteria (1-10 scale):");
            foreach (var c in criteria)
            {
                sb.AppendLine($"- {c.Name}: {c.Description} (Weight: {c.Weight})");
            }
            return sb.ToString();
        }

        private string BuildComparisonPrompt(string userQuery, string responseA, string responseB, List<EvaluationCriterion> criteria)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"User Query: {userQuery}");
            sb.AppendLine();
            sb.AppendLine($"Response A: {responseA}");
            sb.AppendLine();
            sb.AppendLine($"Response B: {responseB}");
            sb.AppendLine();
            sb.AppendLine("Compare based on:");
            foreach (var c in criteria)
            {
                sb.AppendLine($"- {c.Name}: {c.Description}");
            }
            return sb.ToString();
        }

        private EvaluationResult ParseEvaluationResult(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var result = new EvaluationResult { Success = true };

                if (doc.RootElement.TryGetProperty("overall_score", out var score))
                    result.OverallScore = score.GetDouble();

                if (doc.RootElement.TryGetProperty("criteria_scores", out var criteriaScores))
                {
                    foreach (var prop in criteriaScores.EnumerateObject())
                    {
                        result.CriteriaScores[prop.Name] = prop.Value.GetDouble();
                    }
                }

                if (doc.RootElement.TryGetProperty("strengths", out var strengths))
                {
                    foreach (var s in strengths.EnumerateArray())
                    {
                        result.Strengths.Add(s.GetString() ?? string.Empty);
                    }
                }

                if (doc.RootElement.TryGetProperty("weaknesses", out var weaknesses))
                {
                    foreach (var w in weaknesses.EnumerateArray())
                    {
                        result.Weaknesses.Add(w.GetString() ?? string.Empty);
                    }
                }

                if (doc.RootElement.TryGetProperty("reasoning", out var reasoning))
                    result.Reasoning = reasoning.GetString() ?? string.Empty;

                return result;
            }
            catch (Exception ex)
            {
                return new EvaluationResult { Success = false, Error = $"Parse failed: {ex.Message}" };
            }
        }

        private ComparisonResult ParseComparisonResult(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var result = new ComparisonResult { Success = true };

                if (doc.RootElement.TryGetProperty("winner", out var winner))
                    result.Winner = winner.GetString() ?? "Unknown";

                if (doc.RootElement.TryGetProperty("scores", out var scores))
                {
                    foreach (var prop in scores.EnumerateObject())
                    {
                        result.Scores[prop.Name] = prop.Value.GetDouble();
                    }
                }

                if (doc.RootElement.TryGetProperty("reasoning", out var reasoning))
                    result.Reasoning = reasoning.GetString() ?? string.Empty;

                return result;
            }
            catch (Exception ex)
            {
                return new ComparisonResult { Success = false, Error = $"Parse failed: {ex.Message}" };
            }
        }
    }
}