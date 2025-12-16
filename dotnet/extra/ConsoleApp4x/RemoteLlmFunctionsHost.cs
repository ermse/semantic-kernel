// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DicomUtils;
using Microsoft.SemanticKernel;

namespace ConsoleApp4x;

/// <summary>
/// Hosts and executes remote LLM function calls.
/// This class receives serialized function call information and routes it to the appropriate implementation.
/// </summary>
internal class RemoteLlmFunctionsHost
{
    public static readonly string DicomPluginDescriptionJson = CreatePluginDescriptionJson(typeof(DicomPlugin));

    /// <summary>
    /// Creates a JSON description for a plugin type by extracting metadata from methods decorated with KernelFunctionAttribute.
    /// </summary>
    /// <param name="pluginType">The type of the plugin to extract metadata from.</param>
    /// <returns>A JSON string describing the plugin's functions.</returns>
    private static string CreatePluginDescriptionJson(Type pluginType)
    {
        var pluginName = pluginType.Name;
        var functions = new List<RemoteFunctionDescription>();

        var methods = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var kernelFunctionAttr = method.GetCustomAttribute<KernelFunctionAttribute>();
            if (kernelFunctionAttr == null)
            {
                continue;
            }

            var functionName = method.Name;
            if (functionName.EndsWith("Async", StringComparison.Ordinal) && functionName.Length > "Async".Length)
            {
                functionName = functionName.Substring(0, functionName.Length - "Async".Length);
            }

            var descriptionAttr = method.GetCustomAttribute<DescriptionAttribute>();
            var description = descriptionAttr?.Description ?? string.Empty;

            var parameters = new List<RemoteParameterDescription>();
            foreach (var param in method.GetParameters())
            {
                var paramDescAttr = param.GetCustomAttribute<DescriptionAttribute>();
                var paramDesc = paramDescAttr?.Description ?? string.Empty;

                var paramType = GetJsonTypeName(param.ParameterType);

                parameters.Add(new RemoteParameterDescription
                {
                    Name = param.Name ?? string.Empty,
                    Description = paramDesc,
                    Type = paramType,
                    IsRequired = !param.IsOptional && !param.HasDefaultValue
                });
            }

            functions.Add(new RemoteFunctionDescription
            {
                PluginName = pluginName,
                FunctionName = functionName,
                Description = description,
                Parameters = parameters
            });
        }

        if (functions.Count == 0)
        {
            throw new InvalidOperationException($"No functions with KernelFunctionAttribute found in type {pluginType.Name}");
        }

        if (functions.Count == 1)
        {
            return JsonSerializer.Serialize(functions[0]);
        }

        return JsonSerializer.Serialize(functions);
    }

    /// <summary>
    /// Maps a .NET type to a JSON schema type name.
    /// </summary>
    private static string GetJsonTypeName(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string))
        {
            return "string";
        }
        if (underlyingType == typeof(int) || underlyingType == typeof(long) ||
            underlyingType == typeof(short) || underlyingType == typeof(byte) ||
            underlyingType == typeof(uint) || underlyingType == typeof(ulong) ||
            underlyingType == typeof(ushort) || underlyingType == typeof(sbyte))
        {
            return "integer";
        }
        if (underlyingType == typeof(float) || underlyingType == typeof(double) ||
            underlyingType == typeof(decimal))
        {
            return "number";
        }
        if (underlyingType == typeof(bool))
        {
            return "boolean";
        }
        if (underlyingType.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(underlyingType))
        {
            return "array";
        }

        return "object";
    }

    /// <summary>
    /// Executes a function remotely based on serialized function call information.
    /// </summary>
    /// <param name="functionCallJson">JSON containing the plugin name, function name, and arguments.</param>
    /// <param name="cancel">Cancellation token.</param>
    /// <returns>The result of the remote function execution as a string.</returns>
    public async Task<string> ExecuteFunctionAsync(string functionCallJson, CancellationToken cancel)
    {
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

        // Route to appropriate plugin implementation
        if (functionCallData.PluginName == "DicomPlugin" && functionCallData.FunctionName == "RegionsJson")
        {
            return await ExecuteDicomPluginRegionsJsonAsync(functionCallData, cancel);
        }

        throw new NotSupportedException($"Function {functionCallData.PluginName}.{functionCallData.FunctionName} is not supported for remote execution");
    }

    private async Task<string> ExecuteDicomPluginRegionsJsonAsync(FunctionCallData functionCallData, CancellationToken cancel)
    {
        if (functionCallData.Arguments == null)
        {
            throw new InvalidOperationException("Function arguments are null");
        }

        long dicomFileId = ExtractDicomFileId(functionCallData.Arguments);

        Console.WriteLine($"Calling DicomPlugin.RegionsJsonAsync with dicomFileId: {dicomFileId}");

        // This is where the actual remote call would happen
        // In a real scenario, this could be an HTTP call to a remote service
        var dicomPlugin = new DicomPlugin();
        string result = await dicomPlugin.RegionsJsonAsync(dicomFileId);

        return result;
    }

    private long ExtractDicomFileId(KernelArguments arguments)
    {
        long dicomFileId = 0;

        if (arguments.ContainsKey("dicomFileId"))
        {
            var fileIdValue = arguments["dicomFileId"];
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
                    dicomFileId = ExtractFromJsonElement(jsonElement);
                }
                else
                {
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

        return dicomFileId;
    }

    private long ExtractFromJsonElement(JsonElement jsonElement)
    {
        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.Number:
                if (jsonElement.TryGetInt64(out long longVal))
                {
                    return longVal;
                }
                else
                {
                    throw new InvalidOperationException($"Cannot convert JsonElement number to long: {jsonElement}");
                }

            case JsonValueKind.String:
                if (long.TryParse(jsonElement.GetString(), out long parsedLong))
                {
                    return parsedLong;
                }
                else
                {
                    throw new InvalidOperationException($"Cannot parse JsonElement string to long: {jsonElement.GetString()}");
                }

            default:
                throw new InvalidOperationException($"Unexpected JsonElement ValueKind for dicomFileId: {jsonElement.ValueKind}");
        }
    }


    // TODO: replace KernelArguments with our own classes,
    // so remote host do not have to refernece semantic kernel
    /// <summary>
    /// Helper class for deserializing function call data.
    /// </summary>
    private class FunctionCallData
    {
        public string Id { get; set; }
        public string PluginName { get; set; }
        public string FunctionName { get; set; }
        public KernelArguments Arguments { get; set; }
    }
}
