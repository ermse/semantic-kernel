// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using MySkUtils;

namespace ConsoleApp4x;

internal class FunctionCallAutoRemoteTest
{
    public static async Task DoTestAsync(string file, AzureOpenAIConfig azureOpenAIConfig, CancellationToken cancel)
    {
        try
        {
            string result = null;
            ChatHistory chatHistory = null;

            chatHistory = await ChatHistoryDeserializer
                .LoadChatHistoryFromJsonAsync(file, cancel);

            using var httpClient = LlmHttpClientProvider.GetHttpClient(
                TimeSpan.FromSeconds(180),
                3,
                TimeSpan.FromSeconds(5),
                "C:\\tmp\\SemanticKernelDebug\\log.txt");

            Kernel kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: azureOpenAIConfig.Deployment,
                    endpoint: azureOpenAIConfig.Endpoint,
                    apiKey: azureOpenAIConfig.ApiKey,
                    modelId: azureOpenAIConfig.ModelId,
                    httpClient: httpClient)
                .Build();

            // Create remote functions host that will execute functions
            var remoteFunctionsHost = new LlmRemoteFunctionsHost();

            var functionDescriptions = new List<LlmFunctionDescription>
            {
                JsonSerializer.Deserialize<LlmFunctionDescription>(remoteFunctionsHost.DescribeTools())
            };

    

            foreach (var pluginGroup in functionDescriptions.GroupBy(f => f.PluginName))
            {
                var functions = LlmRemoteFunctionWrapper.CreateFunctionsFromDescriptions(pluginGroup.ToList(), remoteFunctionsHost, cancel);
                var plugin = KernelPluginFactory.CreateFromFunctions(pluginGroup.Key, functions);
                kernel.Plugins.Add(plugin);
            }

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

        IReadOnlyList<ChatMessageContent> result = await chatService
            .GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancel)
            .ConfigureAwait(false);

        if (result.Count > 0)
        {
            return result[0].Content;
        }
        throw new InvalidDataException();
    }

    private static void AddFunctionResultToHistory(ChatHistory chatHistory, FunctionCallContent functionCall, string result)
    {
        var functionResultContent = new FunctionResultContent(
            functionName: functionCall.FunctionName,
            pluginName: functionCall.PluginName,
            callId: functionCall.Id,
            result: result);

        var message = new ChatMessageContent(
            role: AuthorRole.Tool,
            content: result);
        message.Items.Add(functionResultContent);

        chatHistory.Add(message);
    }
}
