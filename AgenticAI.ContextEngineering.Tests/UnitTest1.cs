// Tests/Core/ContextCompressorTests.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;                                    // ✅ Add this
using Moq;                                      // ✅ Add this
using FluentAssertions;                        // ✅ Add this
using Microsoft.Extensions.Logging;             // ✅ Add this
using Microsoft.Extensions.Options;             // ✅ Add this
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Services;
using AgenticAI.ContextEngineering.Core.Strategies;

namespace SmartChatBot.ContextEngineering.Tests.Core
{
    public class ContextCompressorTests
    {
        [Fact]  // ✅ Now works with Xunit
        public async Task CompressAsync_ShouldReturnOriginal_WhenWithinBudget()
        {
            // Arrange
            var tokenEstimator = new TokenEstimator();
            var options = Options.Create(new CompressionOptions { MaxTokens = 16000 });
            var logger = new Mock<ILogger<ContextCompressor>>().Object;

            var compressor = new ContextCompressor(
                tokenEstimator,
                new SecretRedactor(),
                new ToolResultPruner(),
                new ProgressiveCompression(tokenEstimator, Mock.Of<ILogger<ProgressiveCompression>>()),
                new AnchorProtection(),
                logger,
                options);

            var context = new CompressableContext
            {
                ConversationId = "test-123",
                Messages = new List<ConversationMessage>
                {
                    new ConversationMessage { Role = "user", Content = "Hello" }
                }
            };

            // Act
            var result = await compressor.CompressAsync(context);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.CompressionRatio.Should().Be(0);
        }

        [Fact]
        public void ShouldCompress_ShouldReturnTrue_WhenOverThreshold()
        {
            // Arrange
            var tokenEstimator = new TokenEstimator();
            var options = Options.Create(new CompressionOptions
            {
                MaxTokens = 100,
                CompressionThreshold = 0.5
            });
            var logger = new Mock<ILogger<ContextCompressor>>().Object;

            var compressor = new ContextCompressor(
                tokenEstimator,
                new SecretRedactor(),
                new ToolResultPruner(),
                new ProgressiveCompression(tokenEstimator, Mock.Of<ILogger<ProgressiveCompression>>()),
                new AnchorProtection(),
                logger,
                options);

            var context = new CompressableContext
            {
                Messages = new List<ConversationMessage>
                {
                    new ConversationMessage { Role = "user", Content = new string('a', 1000) }
                }
            };

            // Act
            var result = compressor.ShouldCompress(context);

            // Assert
            result.Should().BeTrue();
        }
    }
}