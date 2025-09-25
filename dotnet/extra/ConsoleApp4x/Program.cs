using System;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace ConsoleApp4x
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            // Build configuration from appsettings.json, environment variables, and user secrets
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<AzureOpenAIConfig>(optional: false)
                .Build();

            // Read AzureOpenAI configuration
            var azureOpenAIConfig = new AzureOpenAIConfig();
            configuration.GetSection("AzureOpenAIConfig").Bind(azureOpenAIConfig);

            // Validate configuration
            if (string.IsNullOrWhiteSpace(azureOpenAIConfig.ApiKey) ||
                string.IsNullOrWhiteSpace(azureOpenAIConfig.Endpoint) ||
                string.IsNullOrWhiteSpace(azureOpenAIConfig.Deployment) ||
                string.IsNullOrWhiteSpace(azureOpenAIConfig.ModelId))
            {
                throw new InvalidOperationException("AzureOpenAI configuration is missing. Please configure ApiKey, EndPoint, and DeploymentName in appsettings.json or user secrets.");
            }
            var httpClient = new HttpClient();

            // Create kernel with Azure OpenAI configuration
            Kernel kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: azureOpenAIConfig.Deployment,
                    endpoint: azureOpenAIConfig.Endpoint,
                    apiKey: azureOpenAIConfig.ApiKey,
                    modelId: azureOpenAIConfig.ModelId,
                    httpClient: httpClient)
                .Build();

            // Restore chat history from Resource/ChatHistoryDump.json
            ChatHistory chatHistory = await LoadChatHistoryFromJsonAsync("C:/tmp/SemanticKernelDebug/ChatHistoryDump001.json");

            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatService.GetChatMessageContentAsync(chatHistory);
            Console.WriteLine("Kernel created successfully with Azure OpenAI configuration!");
            Console.WriteLine($"Deployment: {azureOpenAIConfig.Deployment}");
            Console.WriteLine($"Endpoint: {azureOpenAIConfig.Endpoint}");
            Console.WriteLine($"Model ID: {azureOpenAIConfig.ModelId ?? "Not specified"}");
            Console.WriteLine($"Chat history loaded with {chatHistory.Count} messages");
        }

        private static async ValueTask<ChatHistory> LoadChatHistoryFromJsonAsync(string filePath)
        {
            var p = Path.GetFullPath(filePath);
            using var stream = File.OpenRead(p);
            var result = await JsonSerializer.DeserializeAsync<ChatHistory>(stream);
            return result;
        }
    }
}
