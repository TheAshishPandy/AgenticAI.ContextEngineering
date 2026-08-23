using AgenticAI.ContextEngineering.Core.Caching;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Reports
{
    public class TokenReportGenerator
    {
        private readonly TokenCache _tokenCache;
        private readonly ILogger<TokenReportGenerator> _logger;

        public TokenReportGenerator(
            TokenCache tokenCache,
            ILogger<TokenReportGenerator> logger)
        {
            _tokenCache = tokenCache;
            _logger = logger;
        }

        public async Task<string> GenerateTokenReportAsync()
        {
            var stats = _tokenCache.GetTokenStats();
            var sb = new StringBuilder();

            sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║           TOKEN CACHE PERFORMANCE REPORT                    ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();

            // Token Statistics
            sb.AppendLine("📊 TOKEN STATISTICS");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine($"  Total Tokens Cached: {stats.TotalTokensCached:N0}");
            sb.AppendLine($"  Total Tokens Saved: {stats.TotalTokensSaved:N0}");
            sb.AppendLine($"  Cache Hit Rate: {stats.CacheHitRate:P2}");
            sb.AppendLine($"  Cost Saved: ${stats.CostSaved:F2}");
            sb.AppendLine();

            // Breakdown by Type
            sb.AppendLine("📈 CACHE BREAKDOWN");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine($"  Prompts Cached: {stats.TotalPromptsCached:N0}");
            sb.AppendLine($"  Completions Cached: {stats.TotalCompletionsCached:N0}");
            sb.AppendLine($"  Embeddings Cached: {stats.TotalEmbeddingsCached:N0}");
            sb.AppendLine();

            // Cost Analysis
            sb.AppendLine("💰 COST ANALYSIS");
            sb.AppendLine(new string('═', 50));
            var costWithoutCache = (stats.TotalTokensSaved + stats.TotalTokensCached) / 1000.0 * 0.02;
            var costWithCache = (stats.TotalTokensCached + stats.TotalTokensSaved - stats.TotalTokensSaved) / 1000.0 * 0.02;
            var savings = stats.CostSaved;

            sb.AppendLine($"  Cost Without Cache: ${costWithoutCache:F2}");
            sb.AppendLine($"  Cost With Cache: ${costWithCache:F2}");
            sb.AppendLine($"  Savings: ${savings:F2}");
            sb.AppendLine($"  Savings Percentage: {(savings / (costWithoutCache + 0.01) * 100):F1}%");
            sb.AppendLine();

            // Recommendations
            sb.AppendLine("💡 RECOMMENDATIONS");
            sb.AppendLine(new string('═', 50));
            if (stats.CacheHitRate < 0.5)
            {
                sb.AppendLine("  ⚠️ Cache hit rate is low. Consider:");
                sb.AppendLine("     • Increase cache TTL");
                sb.AppendLine("     • Pre-warm cache with common queries");
                sb.AppendLine("     • Use semantic caching for similar questions");
            }
            else if (stats.CacheHitRate > 0.8)
            {
                sb.AppendLine("  ✅ Excellent cache hit rate!");
                sb.AppendLine("     • Continue current caching strategy");
                sb.AppendLine("     • Consider adding more cache layers");
            }

            if (stats.CostSaved > 10)
            {
                sb.AppendLine($"  🎉 You've saved ${stats.CostSaved:F2} in token costs!");
            }

            sb.AppendLine();
            sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║  Report generated successfully                              ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");

            return sb.ToString();
        }
    }
}