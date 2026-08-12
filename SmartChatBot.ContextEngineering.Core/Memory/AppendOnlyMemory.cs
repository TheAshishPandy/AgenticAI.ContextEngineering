using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartChatBot.ContextEngineering.Core.Interfaces;
using SmartChatBot.ContextEngineering.Core.Models;

namespace SmartChatBot.ContextEngineering.Core.Memory
{
    public class AppendOnlyMemory : IAppendOnlyMemory
    {
        private readonly string _storagePath;
        private readonly ILogger<AppendOnlyMemory> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public AppendOnlyMemory(string storagePath, ILogger<AppendOnlyMemory> logger)
        {
            _storagePath = storagePath;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            Directory.CreateDirectory(_storagePath);
        }

        public async Task AppendAsync(MemoryEntry entry, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = Path.Combine(_storagePath, $"{entry.ConversationId}.jsonl");
                var json = JsonSerializer.Serialize(entry, _jsonOptions);
                await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to append memory entry");
                throw;
            }
        }

        public async Task<List<MemoryEntry>> ReadAllAsync(
            string conversationId,
            CancellationToken cancellationToken = default)
        {
            var entries = new List<MemoryEntry>();
            var filePath = Path.Combine(_storagePath, $"{conversationId}.jsonl");

            if (!File.Exists(filePath))
                return entries;

            try
            {
                var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var entry = JsonSerializer.Deserialize<MemoryEntry>(line, _jsonOptions);
                        if (entry != null) entries.Add(entry);
                    }
                    catch (JsonException) { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read memory entries");
            }

            return entries;
        }

        public async Task<List<MemoryEntry>> GetRecentAsync(
            string conversationId,
            int count,
            CancellationToken cancellationToken = default)
        {
            var all = await ReadAllAsync(conversationId, cancellationToken);
            return all.OrderByDescending(e => e.Timestamp).Take(count).ToList();
        }

        public async Task<List<MemoryEntry>> SearchAsync(
            string conversationId,
            string query,
            CancellationToken cancellationToken = default)
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