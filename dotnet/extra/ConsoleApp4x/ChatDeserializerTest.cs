// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MySkUtils;
using DicomUtils;

namespace ConsoleApp4x;

internal class ChatDeserializerTest
{
    public static async Task DoTestAsync(string file, AzureOpenAIConfig azureOpenAIConfig, CancellationToken cancel)
    {
        try
        {

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
           
            chatHistory = await ChatHistoryDeserializer
                .LoadChatHistoryFromJsonAsync(file, cancel);

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
            
            // Add DicomPlugin to kernel so LLM can call its functions
            kernel.Plugins.AddFromObject(new DicomPlugin(), "DicomPlugin");

            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            for (int i = 0; i < 20; i++)
            {
                try
                {
                    result = await GetChatResultStreamedAsync(kernel, chatHistory, cancel);
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
