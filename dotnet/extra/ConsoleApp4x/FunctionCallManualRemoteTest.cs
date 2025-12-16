// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    private static readonly string DicomPluginDescriptionJson = @"
    {
        ""PluginName"": ""DicomPlugin"",
        ""FunctionName"": ""RegionsJson"",
        ""Description"": ""Returns string representing json containing [(0018,6011) Sequence of Ultrasound Regions] extracted from dicom file."",
        ""Parameters"": [
            {
                ""Name"": ""dicomFileId"",
                ""Description"": ""Id of the dicom file."",
                ""Type"": ""integer"",
                ""IsRequired"": true
            }
        ]
    }";

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

            var functionDescriptions = new List<RemoteFunctionDescription>
            {
                JsonSerializer.Deserialize<RemoteFunctionDescription>(DicomPluginDescriptionJson)
            };

            // Create remote functions host that will execute functions
            var remoteFunctionsHost = new RemoteLlmFunctionsHost();

            foreach (var pluginGroup in functionDescriptions.GroupBy(f => f.PluginName))
            {
                var functions = CreateFunctionsFromDescriptions(pluginGroup.ToList(), remoteFunctionsHost, cancel);
                var plugin = KernelPluginFactory.CreateFromFunctions(pluginGroup.Key, functions);
                kernel.Plugins.Add(plugin);
            }

            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            try
            {
                result = await GetChatResultAsync(kernel, chatHistory, remoteFunctionsHost, cancel);
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

    private static IEnumerable<KernelFunction> CreateFunctionsFromDescriptions(
        List<RemoteFunctionDescription> descriptions,
        RemoteLlmFunctionsHost remoteFunctionsHost,
        CancellationToken cancel)
    {
        foreach (var desc in descriptions)
        {
            // Create parameter metadata from the textual description
            var parameters = desc.Parameters.Select(p => new KernelParameterMetadata(p.Name)
            {
                Description = p.Description,
                ParameterType = GetTypeFromTypeName(p.Type),
                IsRequired = p.IsRequired
            }).ToList();

            // Capture description for use in the lambda
            var capturedDesc = desc;

            // Create a function that forwards all calls to GetFunctionResultRemotely
            // The function body doesn't contain actual business logic - it just captures
            // the arguments and serializes them to be sent to the remote executor
            var function = KernelFunctionFactory.CreateFromMethod(
                method: async (KernelArguments args) =>
                {
                    // Serialize the function call information
                    var functionCallJson = JsonSerializer.Serialize(new
                    {
                        PluginName = capturedDesc.PluginName,
                        FunctionName = capturedDesc.FunctionName,
                        Arguments = args
                    });

                    Console.WriteLine($"Function {capturedDesc.PluginName}.{capturedDesc.FunctionName} called, forwarding to remote execution...");

                    // Forward to remote execution
                    return await remoteFunctionsHost.ExecuteFunctionAsync(functionCallJson, cancel);
                },
                functionName: desc.FunctionName,
                description: desc.Description,
                parameters: parameters,
                returnParameter: new KernelReturnParameterMetadata
                {
                    ParameterType = typeof(string),
                    Description = "Result from remote function execution"
                }
            );

            yield return function;
        }
    }

    private static Type GetTypeFromTypeName(string typeName)
    {
        return typeName?.ToLowerInvariant() switch
        {
            "integer" => typeof(long),
            "int" => typeof(int),
            "long" => typeof(long),
            "string" => typeof(string),
            "boolean" => typeof(bool),
            "bool" => typeof(bool),
            "number" => typeof(double),
            "double" => typeof(double),
            "float" => typeof(float),
            _ => typeof(object)
        };
    }

    private static async Task<string> GetChatResultAsync(Kernel kernel, ChatHistory chatHistory, RemoteLlmFunctionsHost remoteFunctionsHost, CancellationToken cancel)
    {
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        // Disable auto-invoke to handle function calls manually
        var executionSettings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false)
        };

        const int MaxIterations = 10;
        int iteration = 0;

        while (iteration < MaxIterations)
        {
            iteration++;
            Console.WriteLine($"\n=== Iteration {iteration} ===");

            IReadOnlyList<ChatMessageContent> result = await chatService
                .GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancel)
                .ConfigureAwait(false);

            if (result.Count == 0)
            {
                throw new InvalidDataException("No response from chat service");
            }

            var messageContent = result[0];
            chatHistory.Add(messageContent);

            IEnumerable<FunctionCallContent> functionCalls = FunctionCallContent.GetFunctionCalls(messageContent);

            if (!functionCalls.Any())
            {
                Console.WriteLine("No function calls found. Returning final response.");
                return messageContent.Content ?? string.Empty;
            }

            Console.WriteLine($"Found {functionCalls.Count()} function call(s)");

            foreach (FunctionCallContent functionCall in functionCalls)
            {
                Console.WriteLine($"  Function: {functionCall.PluginName}.{functionCall.FunctionName}");
                Console.WriteLine($"  Arguments: {functionCall.Arguments}");
                Console.WriteLine($"  ID: {functionCall.Id}");

                try
                {
                    if (functionCall.Exception is not null)
                    {
                        string errorMessage = $"Error: Function call processing failed. {functionCall.Exception.Message}";
                        Console.WriteLine($"  Result: {errorMessage}");
                        AddFunctionResultToHistory(chatHistory, functionCall, errorMessage);
                        continue;
                    }

                    var singleFunctionCallJson = JsonSerializer.Serialize(new
                    {
                        Id = functionCall.Id,
                        PluginName = functionCall.PluginName,
                        FunctionName = functionCall.FunctionName,
                        Arguments = functionCall.Arguments
                    });

                    Console.WriteLine($"  Executing function remotely...");
                    string resultValue = await remoteFunctionsHost.ExecuteFunctionAsync(singleFunctionCallJson, cancel);

                    Console.WriteLine($"  Result: {resultValue.Substring(0, Math.Min(200, resultValue.Length))}...");
                    AddFunctionResultToHistory(chatHistory, functionCall, resultValue);
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Error: Exception while invoking function. {ex.Message}";
                    Console.WriteLine($"  Result: {errorMessage}");
                    AddFunctionResultToHistory(chatHistory, functionCall, errorMessage);
                }
            }
        }

        throw new InvalidOperationException($"Maximum iterations ({MaxIterations}) reached without getting a final response.");
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
