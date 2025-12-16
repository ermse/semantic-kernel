// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.SemanticKernel;

namespace ConsoleApp4x;

public class RemoteFunctionWrapper
{
    public static IEnumerable<KernelFunction> CreateFunctionsFromDescriptions(
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

            // NOTE ON AUTOMATIC VS MANUAL FUNCTION INVOCATION:
            // 
            // For remote function execution where you need to track the FunctionCallContent.Id, 
            // you should use MANUAL invocation (as shown in GetChatResultAsync below).
            // 
            // With manual invocation, you have access to the FunctionCallContent which includes the Id:
            //   - You call GetChatMessageContentsAsync with autoInvoke: false
            //   - You extract FunctionCallContent from the response
            //   - You have full access to functionCall.Id, functionCall.Arguments, etc.
            //   - You manually call ExecuteFunctionAsync with the Id included
            //
            // With automatic invocation (autoInvoke: true), the function lambda below is called automatically:
            //   - SK invokes the function internally
            //   - The lambda only receives KernelArguments (the parsed arguments)
            //   - The FunctionCallContent.Id is NOT available in the lambda
            //   - You would need to use a FunctionInvocationFilter to intercept and access the Id
            //
            // WORKAROUND FOR AUTOMATIC INVOCATION:
            // If you need automatic invocation with Id access, use a FunctionInvocationFilter:
            //   1. Add a filter to the kernel that captures the FunctionCallContent
            //   2. The filter can access context.Result.Metadata where SK may store function call info
            //   3. However, this is complex and not recommended for remote execution scenarios
            //
            // RECOMMENDED APPROACH: Use manual invocation (as shown below in GetChatResultAsync)

            var function = KernelFunctionFactory.CreateFromMethod(

                method: async (KernelArguments args, CancellationToken cancel) =>
                {
                    // In automatic invocation mode, this lambda is called by SK
                    // At this point, we only have KernelArguments (parsed arguments)
                    // We do NOT have access to FunctionCallContent.Id here

                    // Serialize the function call information WITHOUT the ID
                    // (ID would need to come from a filter if using automatic invocation)
                    var functionCallJson = JsonSerializer.Serialize(new
                    {
                        Id = (string?)null, // No Id available in automatic invocation without a filter
                        PluginName = capturedDesc.PluginName,
                        FunctionName = capturedDesc.FunctionName,
                        Arguments = args
                    });

                    Console.WriteLine($"Function {capturedDesc.PluginName}.{capturedDesc.FunctionName} called (automatic invocation - Id not available), forwarding to remote execution...");

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
}
