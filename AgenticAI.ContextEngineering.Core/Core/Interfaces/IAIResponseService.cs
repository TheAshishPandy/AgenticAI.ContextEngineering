// Core/Interfaces/IAIResponseService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticAI.ContextEngineering.Core.Models;

namespace AgenticAI.ContextEngineering.Core.Interfaces
{
    public interface IAIResponseService
    {
        /// <summary>
        /// Generate AI response with conversation history and summary provided as arguments
        /// </summary>
        Task<AIResponseResult> GenerateResponseAsync(
            AIResponseRequest request,
            CancellationToken cancellationToken = default);
    }
}