using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ConsoleApp4x
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            try
            {
                var hab = Host.CreateApplicationBuilder(args);

                var host = hab.Build();
                var conf = host.Services.GetRequiredService<IConfiguration>();
                var env = host.Services.GetRequiredService<IHostEnvironment>();
                var lf = host.Services.GetRequiredService<IHostApplicationLifetime>();
                // Build configuration from appsettings.json, environment variables, and user secrets
                //var configuration = new ConfigurationBuilder()
                //    .AddJsonFile("appsettings.json", optional: true)
                //    .AddEnvironmentVariables()
                //    .AddUserSecrets<AzureOpenAIConfig>(optional: false)
                //    .Build();

                // Read AzureOpenAI configuration
                var azureOpenAIConfig = new AzureOpenAIConfig();
                conf.GetSection("AzureOpenAIConfig").Bind(azureOpenAIConfig);

                // Validate configuration
                if (string.IsNullOrWhiteSpace(azureOpenAIConfig.ApiKey) ||
                    string.IsNullOrWhiteSpace(azureOpenAIConfig.Endpoint) ||
                    string.IsNullOrWhiteSpace(azureOpenAIConfig.Deployment) ||
                    string.IsNullOrWhiteSpace(azureOpenAIConfig.ModelId))
                {
                    throw new InvalidOperationException("AzureOpenAI configuration is missing. Please configure ApiKey, EndPoint, and DeploymentName in appsettings.json or user secrets.");
                }
                using var httpClient = new LlmHttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(300);


                // Create kernel with Azure OpenAI configuration
                Kernel kernel = Kernel.CreateBuilder()
                    .AddAzureOpenAIChatCompletion(
                        deploymentName: azureOpenAIConfig.Deployment,
                        endpoint: azureOpenAIConfig.Endpoint,
                        apiKey: azureOpenAIConfig.ApiKey,
                        modelId: azureOpenAIConfig.ModelId,
                        httpClient: httpClient)
                    .Build();

                // Restore chat history from Resources/ChatHistoryDump001.json
                string file = "Resources/ChatHistoryDump-exam-11-video-tiled.json";
                ChatHistory chatHistory = await ChatHistoryDeserializer
                    .LoadChatHistoryFromJsonAsync(file, lf.ApplicationStopping);

                var chatService = kernel.GetRequiredService<IChatCompletionService>();
                var result = await chatService.GetChatMessageContentAsync(chatHistory);
                Console.WriteLine("Kernel created successfully with Azure OpenAI configuration!");
                Console.WriteLine($"Deployment: {azureOpenAIConfig.Deployment}");
                Console.WriteLine($"Endpoint: {azureOpenAIConfig.Endpoint}");
                Console.WriteLine($"Model ID: {azureOpenAIConfig.ModelId ?? "Not specified"}");
                Console.WriteLine($"Chat history loaded with {chatHistory.Count} messages");
                Console.WriteLine($"Response: {result}");
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
