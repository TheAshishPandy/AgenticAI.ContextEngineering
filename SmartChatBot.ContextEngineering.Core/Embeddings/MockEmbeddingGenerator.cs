// Embeddings/MockEmbeddingGenerator.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartChatBot.ContextEngineering.Core.Interfaces;

namespace SmartChatBot.ContextEngineering.Core.Embeddings
{
    /// <summary>
    /// Mock embedding generator for testing (no external dependencies)
    /// </summary>
    public class MockEmbeddingGenerator : IEmbeddingGenerator
    {
        private readonly Random _random = new Random();
        public int Dimensions => 1536;
        public bool IsEnabled => true;

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            // Generate random embedding for testing
            var embedding = new float[Dimensions];
            for (int i = 0; i < Dimensions; i++)
            {
                embedding[i] = (float)(_random.NextDouble() * 2 - 1);
            }
            return Task.FromResult(embedding);
        }

        public Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
        {
            var results = new List<float[]>();
            foreach (var _ in texts)
            {
                results.Add(GenerateEmbeddingAsync("").Result);
            }
            return Task.FromResult(results);
        }
    }
}