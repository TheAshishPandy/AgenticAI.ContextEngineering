// Core/Interfaces/IQdrantClient.cs
using AgenticAI.ContextEngineering.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    public interface IQdrantClient
    {
        Task<List<SearchResult>> SearchAsync(
            float[] queryVector,
            int topK = 10,
            float scoreThreshold = 0.3f,
            Dictionary<string, object>? filters = null,
            CancellationToken cancellationToken = default);

        Task IndexDocumentAsync(
            string id,
            float[] embedding,
            string content,
            string title = "",
            string source = "",
            Dictionary<string, object>? metadata = null,
            CancellationToken cancellationToken = default);

        Task IndexDocumentsAsync(
            List<(string Id, float[] Embedding, string Content, string Title, string Source, Dictionary<string, object> Metadata)> documents,
            CancellationToken cancellationToken = default);

        Task DeleteDocumentAsync(string id, CancellationToken cancellationToken = default);
        Task<long> GetCollectionSizeAsync(CancellationToken cancellationToken = default);
        Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default);
        Task CreateCollectionAsync(int vectorSize = 1536, CancellationToken cancellationToken = default);
    }
}