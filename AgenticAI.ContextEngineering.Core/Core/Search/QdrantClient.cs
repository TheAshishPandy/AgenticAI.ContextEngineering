// Core/Search/QdrantClient.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Search
{
    public class QdrantClient : IQdrantClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _host;
        private readonly string _apiKey;
        private readonly string _collectionName;
        private readonly int _vectorSize;
        private readonly ILogger<QdrantClient> _logger;

        public QdrantClient(
            IConfiguration configuration,
            ILogger<QdrantClient> logger)
        {
            // ✅ Try both configuration keys
            _host = configuration["Qdrant:Host"] ?? configuration["QdrantSettings:Host"]
                ?? throw new InvalidOperationException("Qdrant:Host not configured");

            _apiKey = configuration["Qdrant:ApiKey"] ?? configuration["QdrantSettings:Key"]
                ?? string.Empty;

            _collectionName = configuration["Qdrant:CollectionName"] ?? configuration["QdrantSettings:collectionName"]
                ?? "documents";

            _vectorSize = configuration.GetValue<int>("Qdrant:VectorSize", 1536);
            _logger = logger;

            _httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
            }
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{_host.TrimEnd('/')}/collections/{_collectionName}";
                var response = await _httpClient.GetAsync(url, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task CreateCollectionAsync(int vectorSize = 1536, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{_host.TrimEnd('/')}/collections/{_collectionName}";

                var requestBody = new
                {
                    vectors = new
                    {
                        size = vectorSize,
                        distance = "Cosine"
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(url, content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to create collection: {response.StatusCode} - {responseContent}");
                    throw new Exception($"Failed to create collection: {responseContent}");
                }

                _logger.LogInformation($"Collection '{_collectionName}' created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create collection '{_collectionName}'");
                throw;
            }
        }

        public async Task<List<SearchResult>> SearchAsync(
            float[] queryVector,
            int topK = 10,
            float scoreThreshold = 0.3f,
            Dictionary<string, object>? filters = null,
            CancellationToken cancellationToken = default)
        {
            if (queryVector == null || queryVector.Length == 0)
                return new List<SearchResult>();

            try
            {
                var url = $"{_host.TrimEnd('/')}/collections/{_collectionName}/points/search";

                var requestBody = new
                {
                    vector = queryVector,
                    limit = topK,
                    score_threshold = scoreThreshold,
                    with_payload = true,
                    with_vector = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Qdrant search failed: {response.StatusCode} - {responseContent}");
                    return new List<SearchResult>();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var result = JsonSerializer.Deserialize<QdrantSearchResponse>(responseContent, options);
                var results = new List<SearchResult>();

                if (result?.Result != null && result.Result.Count > 0)
                {
                    foreach (var point in result.Result)
                    {
                        var score = point.Score ?? 0;
                        var payload = point.Payload;

                        string source = "Unknown";
                        string title = string.Empty;
                        string contentValue = string.Empty;
                        string id = point.Id?.ToString() ?? Guid.NewGuid().ToString();

                        if (payload != null)
                        {
                            // Extract common fields
                            if (payload.TryGetValue("source", out var sourceElement))
                            {
                                source = sourceElement.ValueKind == JsonValueKind.String
                                    ? sourceElement.GetString() ?? "Unknown"
                                    : "Unknown";
                            }
                            if (payload.TryGetValue("title", out var titleElement))
                            {
                                title = titleElement.ValueKind == JsonValueKind.String
                                    ? titleElement.GetString() ?? string.Empty
                                    : string.Empty;
                            }
                            if (payload.TryGetValue("content", out var contentElement))
                            {
                                contentValue = contentElement.ValueKind == JsonValueKind.String
                                    ? contentElement.GetString() ?? string.Empty
                                    : string.Empty;
                            }
                            if (payload.TryGetValue("question_id", out var questionIdElement))
                            {
                                if (questionIdElement.ValueKind == JsonValueKind.Number)
                                {
                                    id = questionIdElement.GetInt32().ToString();
                                }
                            }
                            if (payload.TryGetValue("id", out var idElement))
                            {
                                if (idElement.ValueKind == JsonValueKind.String)
                                {
                                    id = idElement.GetString() ?? id;
                                }
                            }
                        }

                        var searchResult = new SearchResult
                        {
                            Id = id,
                            Score = score,
                            Source = source,
                            Title = title,
                            Content = contentValue,
                            Metadata = new Dictionary<string, object>()
                        };

                        if (payload != null)
                        {
                            foreach (var item in payload)
                            {
                                if (item.Key != "content" && item.Key != "title" && item.Key != "source"
                                    && item.Key != "question_id" && item.Key != "id")
                                {
                                    searchResult.Metadata[item.Key] = item.Value.ValueKind == JsonValueKind.String
                                        ? item.Value.GetString() ?? "null"
                                        : item.Value.GetRawText() ?? "null";
                                }
                            }
                        }

                        results.Add(searchResult);
                    }
                }

                _logger.LogDebug($"Qdrant search returned {results.Count} results");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Qdrant search failed");
                return new List<SearchResult>();
            }
        }

        public async Task IndexDocumentAsync(
            string id,
            float[] embedding,
            string content,
            string title = "",
            string source = "",
            Dictionary<string, object>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (embedding == null || embedding.Length == 0)
                throw new ArgumentException("Embedding cannot be empty");

            try
            {
                var url = $"{_host.TrimEnd('/')}/collections/{_collectionName}/points";

                object pointId;
                if (ulong.TryParse(id, out var numericId))
                {
                    pointId = numericId;
                }
                else if (Guid.TryParse(id, out var guidId))
                {
                    pointId = guidId;
                }
                else
                {
                    pointId = (ulong)id.GetHashCode();
                    _logger.LogWarning($"Converted string ID '{id}' to numeric ID {pointId}");
                }

                var payload = new Dictionary<string, object>
                {
                    ["content"] = content,
                    ["title"] = title,
                    ["source"] = source
                };

                if (metadata != null)
                {
                    foreach (var item in metadata)
                    {
                        payload[item.Key] = item.Value;
                    }
                }

                // Add ID to payload for reference
                payload["id"] = id;

                var requestBody = new
                {
                    points = new[]
                    {
                        new
                        {
                            id = pointId,
                            vector = embedding,
                            payload = payload
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var contentJson = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(url, contentJson, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Qdrant index failed: {response.StatusCode} - {responseContent}");
                    throw new Exception($"Failed to index document: {responseContent}");
                }

                _logger.LogDebug($"Document {id} indexed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to index document {id}");
                throw;
            }
        }

        public async Task IndexDocumentsAsync(
            List<(string Id, float[] Embedding, string Content, string Title, string Source, Dictionary<string, object> Metadata)> documents,
            CancellationToken cancellationToken = default)
        {
            foreach (var doc in documents)
            {
                await IndexDocumentAsync(
                    doc.Id,
                    doc.Embedding,
                    doc.Content,
                    doc.Title,
                    doc.Source,
                    doc.Metadata,
                    cancellationToken);
            }
        }

        public async Task DeleteDocumentAsync(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{_host.TrimEnd('/')}/collections/{_collectionName}/points/delete";

                var requestBody = new
                {
                    points = new[] { id }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Qdrant delete failed: {response.StatusCode} - {responseContent}");
                }
                else
                {
                    _logger.LogDebug($"Document {id} deleted successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete document {id}");
            }
        }

        public async Task<long> GetCollectionSizeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{_host.TrimEnd('/')}/collections/{_collectionName}";
                var response = await _httpClient.GetAsync(url, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get collection info: {response.StatusCode}");
                    return 0;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var result = JsonSerializer.Deserialize<QdrantCollectionResponse>(responseContent, options);
                return result?.Result?.PointsCount ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get collection size");
                return 0;
            }
        }

        // ============================================================
        // Response Models
        // ============================================================

        private class QdrantSearchResponse
        {
            [JsonPropertyName("result")]
            public List<QdrantPoint> Result { get; set; } = new();

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("time")]
            public double Time { get; set; }
        }

        private class QdrantPoint
        {
            [JsonPropertyName("id")]
            public object? Id { get; set; }

            [JsonPropertyName("version")]
            public int? Version { get; set; }

            [JsonPropertyName("score")]
            public float? Score { get; set; }

            [JsonPropertyName("payload")]
            public Dictionary<string, JsonElement>? Payload { get; set; }
        }

        private class QdrantCollectionResponse
        {
            [JsonPropertyName("result")]
            public QdrantCollectionInfo Result { get; set; } = new();
        }

        private class QdrantCollectionInfo
        {
            [JsonPropertyName("points_count")]
            public long PointsCount { get; set; }
        }
    }
}