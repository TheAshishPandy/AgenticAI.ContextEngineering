using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    /// <summary>
    /// Append-Only Memory interface
    /// </summary>
    public interface IAppendOnlyMemory
    {
        Task AppendAsync(MemoryEntry entry, CancellationToken cancellationToken = default);
        Task<List<MemoryEntry>> ReadAllAsync(string conversationId, CancellationToken cancellationToken = default);
        Task<List<MemoryEntry>> GetRecentAsync(string conversationId, int count, CancellationToken cancellationToken = default);
        Task<List<MemoryEntry>> SearchAsync(string conversationId, string query, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Long-Term Memory interface
    /// </summary>
    public interface ILongTermMemory
    {
        Task SaveAsync(MemoryEntry entry, CancellationToken cancellationToken = default);
        Task<MemoryEntry> GetAsync(string id, CancellationToken cancellationToken = default);
        Task<List<MemoryEntry>> GetByConversationAsync(string conversationId, CancellationToken cancellationToken = default);
        Task<List<MemoryEntry>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
        Task UpdateAsync(MemoryEntry entry, CancellationToken cancellationToken = default);
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    }
}