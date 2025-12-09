using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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
        internal static string LogFilePath = "C:\\tmp\\SemanticKernelDebug\\log.txt";
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
                string result = null;
                ChatHistory chatHistory = null;
                // Restore chat history from Resources/ChatHistoryDump001.json
                string file = "Resources/ChatHistoryDump-exam-11-video-tiled.json";
                chatHistory = await ChatHistoryDeserializer
                    .LoadChatHistoryFromJsonAsync(file, lf.ApplicationStopping);

                //using var handler = NonDisposableLoggingRetrySocketHttpHandler.Instance;
                // using var handler = new LoggingRetryHttpHandler(logFilePath: LogFilePath);
                // using var handler = new DebugHttpHandler();
                using var httpClient = LlmHttpClientProvider.GetHttpClient();

                // Create kernel with Azure OpenAI configuration
                Kernel kernel = Kernel.CreateBuilder()
                    .AddAzureOpenAIChatCompletion(
                        deploymentName: azureOpenAIConfig.Deployment,
                        endpoint: azureOpenAIConfig.Endpoint,
                        apiKey: azureOpenAIConfig.ApiKey,
                        modelId: azureOpenAIConfig.ModelId,
                        httpClient: httpClient)
                    .Build();


                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        result = await GetChatResultStreamedAsync(kernel, chatHistory, lf.ApplicationStopping);
                    }
                    catch
                    {

                    }
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
                Console.WriteLine("Kernel created successfully with Azure OpenAI configuration!");
                Console.WriteLine($"Deployment: {azureOpenAIConfig.Deployment}");
                Console.WriteLine($"Endpoint: {azureOpenAIConfig.Endpoint}");
                Console.WriteLine($"Model ID: {azureOpenAIConfig.ModelId ?? "Not specified"}");
                Console.WriteLine($"Chat history loaded with {chatHistory.Count} messages");
                Console.WriteLine($"Response: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task<string> GetChatResultAsync(Kernel kernel, ChatHistory chatHistory, CancellationToken cancel)
        {

            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var result = await chatService
                .GetChatMessageContentsAsync(chatHistory, cancellationToken: cancel)
                .ConfigureAwait(false);

            if (result.Count > 0)
            {
                return result[0].Content;
            }
            throw new InvalidDataException();
        }

        private static async Task<string> GetChatResultStreamedAsync(Kernel kernel, ChatHistory chatHistory, CancellationToken cancel)
        {

            var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

            IAsyncEnumerable<StreamingChatMessageContent> resp = chatCompletion
                .GetStreamingChatMessageContentsAsync(chatHistory, null, null, cancel);

            var str = new StringBuilder();

            await foreach (StreamingChatMessageContent item in resp)
            {
                if (item != null && !string.IsNullOrEmpty(item.Content))
                {
                    str.Append(item.Content);
                }
            }
            return str.ToString();
        }
    }
}
