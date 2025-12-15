// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DicomUtils;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using MySkUtils;

namespace ConsoleApp4x;

internal class FunctionCallManualRemoteTest
{
    public static async Task DoTestAsync(string file, AzureOpenAIConfig azureOpenAIConfig, CancellationToken cancel)
    {
        try
        {
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

        // Disable auto-invoke to handle function calls manually
        var executionSettings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false)
        };

        const int MaxIterations = 10; // Prevent infinite loops
        int iteration = 0;

        while (iteration < MaxIterations)
        {
            iteration++;
            Console.WriteLine($"\n=== Iteration {iteration} ===");

            // Get response from LLM
            IReadOnlyList<ChatMessageContent> result = await chatService
                .GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancel)
                .ConfigureAwait(false);

            if (result.Count == 0)
            {
                throw new InvalidDataException("No response from chat service");
            }

            var messageContent = result[0];

            // Add the assistant's response to chat history
            chatHistory.Add(messageContent);

            // Inspect the response for function calls
            IEnumerable<FunctionCallContent> functionCalls = FunctionCallContent.GetFunctionCalls(messageContent);

            if (!functionCalls.Any())
            {
                // No function calls - we have the final response
                Console.WriteLine("No function calls found. Returning final response.");
                return messageContent.Content ?? string.Empty;
            }

            // Process each function call
            Console.WriteLine($"Found {functionCalls.Count()} function call(s)");

            foreach (FunctionCallContent functionCall in functionCalls)
            {
                Console.WriteLine($"  Function: {functionCall.PluginName}.{functionCall.FunctionName}");
                Console.WriteLine($"  Arguments: {functionCall.Arguments}");
                Console.WriteLine($"  ID: {functionCall.Id}");

                try
                {
                    // Validate the function call
                    if (functionCall.Exception is not null)
                    {
                        string errorMessage = $"Error: Function call processing failed. {functionCall.Exception.Message}";
                        Console.WriteLine($"  Result: {errorMessage}");
                        AddFunctionResultToHistory(chatHistory, functionCall, errorMessage);
                        continue;
                    }

                    // Look up the function in the kernel
                    if (!kernel.Plugins.TryGetFunction(functionCall.PluginName, functionCall.FunctionName, out KernelFunction? function))
                    {
                        string errorMessage = $"Error: Function {functionCall.PluginName}.{functionCall.FunctionName} not found in kernel.";
                        Console.WriteLine($"  Result: {errorMessage}");
                        AddFunctionResultToHistory(chatHistory, functionCall, errorMessage);
                        continue;
                    }

                    // Invoke the function
                    Console.WriteLine($"  Invoking function...");
                    FunctionResult functionResult = await function.InvokeAsync(kernel, functionCall.Arguments, cancel);

                    string resultValue = functionResult.GetValue<object>()?.ToString() ?? string.Empty;
                    Console.WriteLine($"  Result: {resultValue.Substring(0, Math.Min(200, resultValue.Length))}...");

                    // Add the function result to chat history
                    AddFunctionResultToHistory(chatHistory, functionCall, resultValue);
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Error: Exception while invoking function. {ex.Message}";
                    Console.WriteLine($"  Result: {errorMessage}");
                    AddFunctionResultToHistory(chatHistory, functionCall, errorMessage);
                }
            }

            // Continue the loop to send function results back to the LLM
        }

        throw new InvalidOperationException($"Maximum iterations ({MaxIterations}) reached without getting a final response.");
    }

    // TODO
    // change the method GetChatResultAsync so serializes function call instead of invoking,
    // similar to how Llm in response does it. 
    // and calls GetFunctionResultRemotely passing it serialized array of function calls
    // the GetFunctionResultRemotely calls new DicomPlugin().RegionsJsonAsync(...)
    // filling the call with required parameters and returning result.
    private static async Task<string> GetFunctionResultRemotely(string functionCallJson, CancellationToken cancel)
    {
        
    }

    private static void AddFunctionResultToHistory(ChatHistory chatHistory, FunctionCallContent functionCall, string result)
    {
        // Create a FunctionResultContent
        var functionResultContent = new FunctionResultContent(
            functionName: functionCall.FunctionName,
            pluginName: functionCall.PluginName,
            callId: functionCall.Id,
            result: result);

        // Create a chat message with the tool role and add the function result
        var message = new ChatMessageContent(
            role: AuthorRole.Tool,
            content: result);
        message.Items.Add(functionResultContent);

        chatHistory.Add(message);
    }
}
