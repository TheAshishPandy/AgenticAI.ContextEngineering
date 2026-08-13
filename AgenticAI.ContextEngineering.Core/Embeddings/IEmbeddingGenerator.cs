// Core/Interfaces/IEmbeddingGenerator.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    public interface IEmbeddingGenerator
    {
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
        Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);
        int Dimensions { get; }
        bool IsEnabled { get; }
    }
}

