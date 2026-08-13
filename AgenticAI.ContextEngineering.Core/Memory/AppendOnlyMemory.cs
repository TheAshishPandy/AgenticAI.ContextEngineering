// Memory/AppendOnlyMemory.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Memory
{
    public interface IAppendOnlyMemory
    {
        Task AppendAsync(MemoryEntry entry, CancellationToken cancellationToken = default);
        Task<List<MemoryEntry>> ReadAllAsync(string conversationId, CancellationToken cancellationToken = default);
        Task<List<MemoryEntry>> GetRecentAsync(string conversationId, int count, CancellationToken cancellationToken = default);
        Task<List<MemoryEntry>> SearchAsync(string conversationId, string query, CancellationToken cancellationToken = default);
    }

    public class MemoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ConversationId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public MemoryType Type { get; set; }
        public double ImportanceScore { get; set; } = 0.5;
    }

    public enum MemoryType
    {
        Fact,
        Preference,
        Decision,
        Intent,
        Context,
        Entity,
        Response
    }

    public class AppendOnlyMemory : IAppendOnlyMemory
    {
        private readonly string _storagePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public AppendOnlyMemory(MemoryOptions options)
        {
            _storagePath = options.StoragePath;
            Directory.CreateDirectory(_storagePath);
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task AppendAsync(MemoryEntry entry, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_storagePath, $"{entry.ConversationId}.jsonl");
            var json = JsonSerializer.Serialize(entry, _jsonOptions);
            await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
        }

        public async Task<List<MemoryEntry>> ReadAllAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            var entries = new List<MemoryEntry>();
            var filePath = Path.Combine(_storagePath, $"{conversationId}.jsonl");

            if (!File.Exists(filePath))
                return entries;

            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<MemoryEntry>(line, _jsonOptions);
                    if (entry != null) entries.Add(entry);
                }
                catch { }
            }

            return entries;
        }

        public async Task<List<MemoryEntry>> GetRecentAsync(string conversationId, int count, CancellationToken cancellationToken = default)
        {
            var all = await ReadAllAsync(conversationId, cancellationToken);
            return all.OrderByDescending(e => e.Timestamp).Take(count).ToList();
        }

        public async Task<List<MemoryEntry>> SearchAsync(string conversationId, string query, CancellationToken cancellationToken = default)
        {
            var all = await ReadAllAsync(conversationId, cancellationToken);
            var lowerQuery = query.ToLowerInvariant();

            return all
                .Where(e => e.Content.ToLowerInvariant().Contains(lowerQuery) ||
                           e.Key.ToLowerInvariant().Contains(lowerQuery))
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }
    }
}