using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartChatBot.ContextEngineering.Core.Models;

namespace SmartChatBot.ContextEngineering.Core.Interfaces
{
    /// <summary>
    /// LLM-as-Judge interface
    /// </summary>
    public interface ILLMAsJudge
    {
        Task<EvaluationResult> EvaluateAsync(
            string userQuery,
            string response,
            List<EvaluationCriterion> criteria,
            CancellationToken cancellationToken = default);

        Task<ComparisonResult> CompareAsync(
            string userQuery,
            string responseA,
            string responseB,
            List<EvaluationCriterion> criteria,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Degradation Detector interface
    /// </summary>
    public interface IDegradationDetector
    {
        Task<bool> DetectAsync(
            string conversationId,
            List<double> confidenceScores,
            List<int> tokenUsage,
            CancellationToken cancellationToken = default);
        Task<DegradationReport> GetReportAsync(string conversationId, CancellationToken cancellationToken = default);
    }
}