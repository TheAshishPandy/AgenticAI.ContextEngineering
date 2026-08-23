// AgenticAI.ContextEngineering.Core/Extensions/AIResponseServiceExtensions.cs
using AgenticAI.ContextEngineering.Core.Interfaces;
using AgenticAI.ContextEngineering.Core.Models;
using AgenticAI.ContextEngineering.Core.Services;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.ClientModel;

namespace AgenticAI.ContextEngineering.Core.Extensions
{
    public static class AIResponseServiceExtensions
    {
        public static IServiceCollection AddAIResponseService(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSection = "AIResponse")
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Configure options
            services.Configure<AIResponseOptions>(options =>
            {
                options.Endpoint = configuration[$"{configSection}:Endpoint"] ?? configuration["AzureOpenAI:Endpoint"] ?? string.Empty;
                options.ApiKey = configuration[$"{configSection}:ApiKey"] ?? configuration["AzureOpenAI:Key"] ?? string.Empty;
                options.DeploymentName = configuration[$"{configSection}:DeploymentName"] ?? configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4";
                options.Temperature = configuration.GetValue<float>($"{configSection}:Temperature", 0.7f);
                options.MaxTokens = configuration.GetValue<int>($"{configSection}:MaxTokens", 500);
                options.SystemPrompt = configuration[$"{configSection}:SystemPrompt"] ?? "You are a helpful assistant.";
            });

            // Register AzureOpenAIClient
            services.AddSingleton<AzureOpenAIClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AIResponseOptions>>().Value;

                if (string.IsNullOrEmpty(options.Endpoint) || string.IsNullOrEmpty(options.ApiKey))
                {
                    var logger = sp.GetService<ILogger<AIResponseService>>();
                    logger?.LogWarning("⚠️ Azure OpenAI credentials not configured.");
                    return null!;
                }

                try
                {
                    return new AzureOpenAIClient(
                        new Uri(options.Endpoint),
                        new ApiKeyCredential(options.ApiKey));
                }
                catch (Exception ex)
                {
                    var logger = sp.GetService<ILogger<AIResponseService>>();
                    logger?.LogError(ex, "❌ Failed to create AzureOpenAIClient");
                    return null!;
                }
            });

            // ✅ Simple registration - just the service
            services.AddScoped<IAIResponseService, AIResponseService>();

            return services;
        }
    }
}