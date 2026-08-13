using System;
using System.Collections.Generic;

namespace AgenticAI.ContextEngineering.Core.Models
{
    /// <summary>
    /// Evaluation criterion
    /// </summary>
    public class EvaluationCriterion
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Weight { get; set; } = 1.0;
        public int Scale { get; set; } = 10;
    }

    /// <summary>
    /// Evaluation result
    /// </summary>
    public class EvaluationResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
        public double OverallScore { get; set; }
        public Dictionary<string, double> CriteriaScores { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public string Reasoning { get; set; } = string.Empty;
        public string Suggestions { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comparison result
    /// </summary>
    public class ComparisonResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
        public string Winner { get; set; } = string.Empty;
        public Dictionary<string, double> Scores { get; set; } = new();
        public string Reasoning { get; set; } = string.Empty;
    }

    /// <summary>
    /// Degradation report
    /// </summary>
    public class DegradationReport
    {
        public string ConversationId { get; set; } = string.Empty;
        public bool IsDegraded { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<string> Patterns { get; set; } = new();
        public Dictionary<string, double> Metrics { get; set; } = new();
        public DateTime ReportGenerated { get; set; } = DateTime.UtcNow;
    }
}