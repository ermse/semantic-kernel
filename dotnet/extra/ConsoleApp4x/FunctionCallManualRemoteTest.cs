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

            /*
            TODO:
            instead of adding functions to kernel by instatiating DicomPlugin
           
            take some sort of textual description of the plugin and it's functions
            and make sure that textual representation is included in llm request like in sample data below.
            So llm can make function calls.
            The idea is to somehow add text information to the Llm request from function description without
            having actual function and if Llm response wiht funcion call get that textual information and
            pass it to 
            GetFunctionResultRemotely(string functionCallJson, CancellationToken cancel)



            {
             ...
             "tools": [
                {
                  "type": "function",
                  "function": {
                    "description": "Returns string representing json containing [(0018,6011) Sequence of Ultrasound Regions] extracted from dicom file.",
                    "name": "DicomPlugin-RegionsJson",
                    "parameters": {
                      "type": "object",
                      "required": [
                        "dicomFileId"
                      ],
                      "properties": {
                        "dicomFileId": {
                          "description": "Id of the dicom file.",
                          "type": "integer"
                        }
                      }
                    },
                    "strict": false
                  }
                }
              ],
              "tool_choice": "auto"
            ...
            }


            */

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

            // Process each function call - serialize and call remotely
            Console.WriteLine($"Found {functionCalls.Count()} function call(s)");

            // Serialize function calls to JSON array
            var serializedFunctionCalls = functionCalls.Select(fc => new
            {
                Id = fc.Id,
                PluginName = fc.PluginName,
                FunctionName = fc.FunctionName,
                Arguments = fc.Arguments
            }).ToList();

            string functionCallsJson = JsonSerializer.Serialize(serializedFunctionCalls);
            Console.WriteLine($"Serialized function calls: {functionCallsJson}");

            // Call remote function execution for each function call
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

                    // Serialize this specific function call
                    var singleFunctionCallJson = JsonSerializer.Serialize(new
                    {
                        Id = functionCall.Id,
                        PluginName = functionCall.PluginName,
                        FunctionName = functionCall.FunctionName,
                        Arguments = functionCall.Arguments
                    });

                    // Execute the function remotely
                    Console.WriteLine($"  Executing function remotely...");
                    string resultValue = await GetFunctionResultRemotely(singleFunctionCallJson, cancel);

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

    private static async Task<string> GetFunctionResultRemotely(string functionCallJson, CancellationToken cancel)
    {
        // Deserialize the function call
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var functionCallData = JsonSerializer.Deserialize<FunctionCallData>(functionCallJson, options);

        if (functionCallData == null)
        {
            throw new InvalidOperationException("Failed to deserialize function call JSON");
        }

        Console.WriteLine($"Remote execution - Plugin: {functionCallData.PluginName}, Function: {functionCallData.FunctionName}");

        // Check if this is a DicomPlugin call
        if (functionCallData.PluginName == "DicomPlugin" && functionCallData.FunctionName == "RegionsJson")
        {
            // Extract the dicomFileId parameter from Arguments
            if (functionCallData.Arguments == null)
            {
                throw new InvalidOperationException("Function arguments are null");
            }

            long dicomFileId = 0;

            // Arguments is a KernelArguments which is essentially a dictionary
            if (functionCallData.Arguments.ContainsKey("dicomFileId"))
            {
                var fileIdValue = functionCallData.Arguments["dicomFileId"];
                if (fileIdValue != null)
                {
                    if (fileIdValue is long longValue)
                    {
                        dicomFileId = longValue;
                    }
                    else if (fileIdValue is int intValue)
                    {
                        dicomFileId = intValue;
                    }
                    else if (fileIdValue is string strValue && long.TryParse(strValue, out long parsedValue))
                    {
                        dicomFileId = parsedValue;
                    }
                    else if (fileIdValue is JsonElement jsonElement)
                    {
                        // Handle JsonElement properly based on its ValueKind
                        switch (jsonElement.ValueKind)
                        {
                            case JsonValueKind.Number:
                                if (jsonElement.TryGetInt64(out long longVal))
                                {
                                    dicomFileId = longVal;
                                }
                                else
                                {
                                    throw new InvalidOperationException($"Cannot convert JsonElement number to long: {jsonElement}");
                                }
                                break;
                            case JsonValueKind.String:
                                if (long.TryParse(jsonElement.GetString(), out long parsedLong))
                                {
                                    dicomFileId = parsedLong;
                                }
                                else
                                {
                                    throw new InvalidOperationException($"Cannot parse JsonElement string to long: {jsonElement.GetString()}");
                                }
                                break;
                            default:
                                throw new InvalidOperationException($"Unexpected JsonElement ValueKind for dicomFileId: {jsonElement.ValueKind}");
                        }
                    }
                    else
                    {
                        // Fallback: try using Convert.ToInt64 for other types
                        try
                        {
                            dicomFileId = Convert.ToInt64(fileIdValue);
                        }
                        catch (InvalidCastException ex)
                        {
                            throw new InvalidOperationException($"Cannot convert dicomFileId value of type {fileIdValue.GetType()} to long", ex);
                        }
                    }
                }
            }

            Console.WriteLine($"Calling DicomPlugin.RegionsJsonAsync with dicomFileId: {dicomFileId}");

            // Create a new instance of DicomPlugin and call the function
            var dicomPlugin = new DicomPlugin();
            string result = await dicomPlugin.RegionsJsonAsync(dicomFileId);

            return result;
        }

        throw new NotSupportedException($"Function {functionCallData.PluginName}.{functionCallData.FunctionName} is not supported for remote execution");
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

    // Helper class for deserializing function call data
    private class FunctionCallData
    {
        public string? Id { get; set; }
        public string? PluginName { get; set; }
        public string? FunctionName { get; set; }
        public KernelArguments? Arguments { get; set; }
    }
}
