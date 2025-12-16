// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace ConsoleApp4x;

public class LlmPluginDescriber
{
    /// <summary>
    /// Creates a JSON description for a plugin type by extracting metadata from methods decorated with KernelFunctionAttribute.
    /// </summary>
    /// <param name="pluginType">The type of the plugin to extract metadata from.</param>
    /// <returns>A JSON string describing the plugin's functions.</returns>
    public static string CreatePluginDescriptionJson(Type pluginType)
    {
        var pluginName = pluginType.Name;
        var functions = new List<LlmFunctionDescription>();

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

            var parameters = new List<LlmFunctionParameterDescription>();
            foreach (var param in method.GetParameters())
            {
                var paramDescAttr = param.GetCustomAttribute<DescriptionAttribute>();
                var paramDesc = paramDescAttr?.Description ?? string.Empty;

                var paramType = GetJsonTypeName(param.ParameterType);

                parameters.Add(new LlmFunctionParameterDescription
                {
                    Name = param.Name ?? string.Empty,
                    Description = paramDesc,
                    Type = paramType,
                    IsRequired = !param.IsOptional && !param.HasDefaultValue
                });
            }

            functions.Add(new LlmFunctionDescription
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

}
