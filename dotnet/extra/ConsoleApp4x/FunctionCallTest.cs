// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DicomUtils;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using MySkUtils;

namespace ConsoleApp4x;

internal class FunctionCallTest
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


            using var httpClient = LlmHttpClientProvider.GetHttpClient(
                TimeSpan.FromSeconds(180),
                3,
                TimeSpan.FromSeconds(5),
                "C:\\tmp\\SemanticKernelDebug\\log.txt");



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


            try
            {
                result = await GetChatResultAsync(kernel, chatHistory, cancel);
            }
            catch
            {

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

        var executionSettings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var result = await chatService
            .GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancel)
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

        var executionSettings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        IAsyncEnumerable<StreamingChatMessageContent> resp = chatCompletion
            .GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancel);

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
