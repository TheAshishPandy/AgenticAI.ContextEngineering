// AgenticAI.ContextEngineering.Core/Core/Report/CacheReportGenerator.cs
using AgenticAI.ContextEngineering.Core.Caching;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Core.Report
{
    public class CacheReportGenerator
    {
        private readonly MultiTierKVCache _cache;
        private readonly ILogger<CacheReportGenerator> _logger;
        private readonly List<CacheHitRateHistory> _hitRateHistory = new();
        private readonly List<CacheSizeHistory> _sizeHistory = new();

        public CacheReportGenerator(MultiTierKVCache cache, ILogger<CacheReportGenerator> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<CacheReport> GenerateReportAsync()
        {
            var report = new CacheReport
            {
                GeneratedAt = DateTime.UtcNow,
                ApplicationName = "Agentic AI ",
                Tiers = _cache.GetAllTierStatistics(),
                HitRateHistory = _hitRateHistory.ToList(),
                SizeHistory = _sizeHistory.ToList()
            };

            // Calculate total statistics
            report.Total = new CacheStatistics
            {
                CacheName = "Total",
                TotalItems = report.Tiers.Values.Sum(t => t.TotalItems),
                TotalSizeBytes = report.Tiers.Values.Sum(t => t.TotalSizeBytes),
                Hits = report.Tiers.Values.Sum(t => t.Hits),
                Misses = report.Tiers.Values.Sum(t => t.Misses),
                Evictions = report.Tiers.Values.Sum(t => t.Evictions),
                LastUpdated = DateTime.UtcNow,
                IsConnected = report.Tiers.Values.All(t => t.IsConnected),
                ConnectionStatus = report.Tiers.Values.All(t => t.IsConnected) ? "Connected" : "Partial"
            };

            // Check distributed cache health
            if (report.Tiers.TryGetValue("Distributed", out var distributedStats))
            {
                report.DistributedCacheHealth = new DistributedCacheHealth
                {
                    IsConnected = distributedStats.IsConnected,
                    Status = distributedStats.ConnectionStatus ?? "Unknown",
                    LastCheck = distributedStats.LastUpdated,
                    ErrorMessage = distributedStats.Metadata?.GetValueOrDefault("ErrorMessage") as string ?? string.Empty
                };
            }

            // Generate recommendations
            report.Recommendations = GenerateRecommendations(report);

            // Record history
            _hitRateHistory.Add(new CacheHitRateHistory
            {
                Timestamp = DateTime.UtcNow,
                HitRate = report.OverallHitRate,
                Tier = "Overall"
            });

            _sizeHistory.Add(new CacheSizeHistory
            {
                Timestamp = DateTime.UtcNow,
                SizeBytes = report.TotalSize,
                ItemCount = report.TotalItems,
                Tier = "Overall"
            });

            return report;
        }

        private Dictionary<string, object> GenerateRecommendations(CacheReport report)
        {
            var recommendations = new Dictionary<string, object>();

            // Check distributed cache health
            if (report.Tiers.TryGetValue("Distributed", out var distributedStats) && !distributedStats.IsConnected)
            {
                recommendations["DistributedCache"] = new
                {
                    Severity = "Critical",
                    Message = $"Distributed cache is not connected. Status: {distributedStats.ConnectionStatus}",
                    RecommendedAction = "Check Redis/SQL Server connection and restart the cache service."
                };
            }

            // Analyze hit rates
            foreach (var tier in report.Tiers)
            {
                if (tier.Value.HitRate < 0.3 && tier.Key != "Distributed")
                {
                    recommendations[$"{tier.Key}_HitRate"] = new
                    {
                        Severity = "Warning",
                        Message = $"Low hit rate ({tier.Value.HitRate:P2}). Consider increasing TTL.",
                        CurrentValue = tier.Value.HitRate,
                        RecommendedAction = "Increase TTL or review cache key strategy"
                    };
                }
                else if (tier.Value.HitRate > 0.8)
                {
                    recommendations[$"{tier.Key}_HitRate"] = new
                    {
                        Severity = "Info",
                        Message = $"Excellent hit rate ({tier.Value.HitRate:P2}).",
                        CurrentValue = tier.Value.HitRate,
                        RecommendedAction = "Keep current configuration"
                    };
                }
            }

            // Analyze size
            if (report.TotalSize > 1024 * 1024 * 100) // 100MB
            {
                recommendations["TotalSize"] = new
                {
                    Severity = "Warning",
                    Message = $"Large cache size ({report.TotalSize / (1024 * 1024)} MB).",
                    CurrentValue = report.TotalSize,
                    RecommendedAction = "Implement LRU eviction or reduce TTL"
                };
            }

            return recommendations;
        }

        public async Task<string> GenerateReportStringAsync()
        {
            var report = await GenerateReportAsync();
            var sb = new StringBuilder();

            sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║           CACHE PERFORMANCE REPORT                          ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Application: {report.ApplicationName}");
            sb.AppendLine();

            // Distributed Cache Health
            sb.AppendLine("🌐 DISTRIBUTED CACHE HEALTH");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine($"  Status: {report.DistributedCacheHealth.Status}");
            sb.AppendLine($"  Connected: {(report.DistributedCacheHealth.IsConnected ? "✅ Yes" : "❌ No")}");
            sb.AppendLine($"  Last Check: {report.DistributedCacheHealth.LastCheck:yyyy-MM-dd HH:mm:ss}");
            if (!string.IsNullOrEmpty(report.DistributedCacheHealth.ErrorMessage))
            {
                sb.AppendLine($"  Error: {report.DistributedCacheHealth.ErrorMessage}");
            }
            sb.AppendLine();

            // Overall Summary
            sb.AppendLine("📊 OVERALL SUMMARY");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine($"  Total Items: {report.TotalItems:N0}");
            sb.AppendLine($"  Total Size: {FormatBytes(report.TotalSize)}");
            sb.AppendLine($"  Overall Hit Rate: {report.OverallHitRate:P2}");
            sb.AppendLine();

            // Tier Details
            sb.AppendLine("📈 TIER DETAILS");
            sb.AppendLine(new string('═', 50));
            foreach (var tier in report.Tiers)
            {
                var statusIcon = tier.Value.IsConnected ? "🟢" : "🔴";
                sb.AppendLine($"  {statusIcon} {tier.Key.ToUpper()}:");
                sb.AppendLine($"    Items: {tier.Value.TotalItems:N0}");
                sb.AppendLine($"    Size: {FormatBytes(tier.Value.TotalSizeBytes)}");
                sb.AppendLine($"    Hits: {tier.Value.Hits:N0}");
                sb.AppendLine($"    Misses: {tier.Value.Misses:N0}");
                sb.AppendLine($"    Hit Rate: {tier.Value.HitRate:P2}");
                sb.AppendLine($"    Evictions: {tier.Value.Evictions:N0}");
                if (!tier.Value.IsConnected)
                {
                    sb.AppendLine($"    Status: ❌ {tier.Value.ConnectionStatus}");
                }
                sb.AppendLine();
            }

            // Recommendations
            if (report.Recommendations.Any())
            {
                sb.AppendLine("💡 RECOMMENDATIONS");
                sb.AppendLine(new string('═', 50));
                foreach (var rec in report.Recommendations)
                {
                    try
                    {
                        var recObj = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            JsonSerializer.Serialize(rec.Value));
                        if (recObj != null)
                        {
                            var severity = recObj.GetValueOrDefault("Severity", "Info") as string;
                            var message = recObj.GetValueOrDefault("Message", "") as string;
                            var action = recObj.GetValueOrDefault("RecommendedAction", "") as string;
                            var severityIcon = severity switch
                            {
                                "Critical" => "🔴",
                                "Warning" => "🟡",
                                _ => "🟢"
                            };
                            sb.AppendLine($"  {severityIcon} [{severity}] {rec.Key}");
                            sb.AppendLine($"    {message}");
                            if (!string.IsNullOrEmpty(action))
                                sb.AppendLine($"    → {action}");
                            sb.AppendLine();
                        }
                    }
                    catch
                    {
                        // Skip invalid recommendations
                    }
                }
            }

            sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║  Report generated successfully                              ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");

            return sb.ToString();
        }

        public async Task SaveReportToJsonAsync(string filePath)
        {
            var report = await GenerateReportAsync();
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation($"📁 JSON report saved to: {filePath}");
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}