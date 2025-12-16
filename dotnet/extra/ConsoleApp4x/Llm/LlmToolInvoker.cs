// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp4x;

/// <summary>
/// Handles the invocation of LLM tool functions using reflection.
/// This class is responsible for executing function calls based on function call data.
/// </summary>
public class LlmToolInvoker
{
    private readonly LlmToolsRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmToolInvoker"/> class.
    /// </summary>
    /// <param name="registry">The tools registry containing registered tools and service provider.</param>
    public LlmToolInvoker(LlmToolsRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Executes a function based on the provided function call data.
    /// </summary>
    /// <param name="functionCallData">The function call data containing plugin name, function name, and arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the function execution as a string.</returns>
    public async Task<string> ExecuteFunctionAsync(LlmToolCallInfo functionCallData, CancellationToken cancellationToken)
    {
        if (functionCallData == null)
        {
            throw new ArgumentNullException(nameof(functionCallData));
        }

        // Find the tool type by plugin name
        var toolType = _registry.Tools.FirstOrDefault(t =>
            string.Equals(t.Name, functionCallData.PluginName, StringComparison.OrdinalIgnoreCase));

        if (toolType == null)
        {
            throw new InvalidOperationException($"Plugin '{functionCallData.PluginName}' is not registered.");
        }

        // Get the service instance
        var serviceProvider = _registry.GetServiceProvider();
        var serviceInstance = serviceProvider.GetService(toolType);

        if (serviceInstance == null)
        {
            throw new InvalidOperationException($"Failed to resolve service for plugin '{functionCallData.PluginName}'.");
        }

        // Find the method by function name (check both with and without Async suffix)
        var method = FindMethod(toolType, functionCallData.FunctionName);

        if (method == null)
        {
            throw new InvalidOperationException($"Function '{functionCallData.FunctionName}' not found on plugin '{functionCallData.PluginName}'.");
        }

        // Build method parameters
        var parameters = BuildMethodParameters(method, functionCallData.Arguments, cancellationToken);

        // Invoke the method
        var result = method.Invoke(serviceInstance, parameters);

        // Handle async methods
        if (result is Task task)
        {
            await task.ConfigureAwait(false);

            // Get the result from Task<T>
            var taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                var resultProperty = taskType.GetProperty("Result");
                if (resultProperty != null)
                {
                    var taskResult = resultProperty.GetValue(task);
                    return ConvertResultToString(taskResult);
                }
            }

            return string.Empty;
        }

        return ConvertResultToString(result);
    }

    private static MethodInfo FindMethod(Type type, string functionName)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        // Try exact match first
        var method = methods.FirstOrDefault(m =>
            string.Equals(m.Name, functionName, StringComparison.OrdinalIgnoreCase));

        if (method != null)
        {
            return method;
        }

        // Try with Async suffix
        method = methods.FirstOrDefault(m =>
            string.Equals(m.Name, functionName + "Async", StringComparison.OrdinalIgnoreCase));

        return method;
    }

    private static object[] BuildMethodParameters(MethodInfo method, Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var methodParams = method.GetParameters();
        var parameterValues = new object[methodParams.Length];

        for (int i = 0; i < methodParams.Length; i++)
        {
            var param = methodParams[i];

            // Handle CancellationToken parameter
            if (param.ParameterType == typeof(CancellationToken))
            {
                parameterValues[i] = cancellationToken;
                continue;
            }

            // Try to get value from arguments
            object argValue = null;
            bool found = false;

            if (arguments != null)
            {
                foreach (var kvp in arguments)
                {
                    if (string.Equals(kvp.Key, param.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        argValue = kvp.Value;
                        found = true;
                        break;
                    }
                }
            }

            if (found && argValue != null)
            {
                parameterValues[i] = ConvertArgument(argValue, param.ParameterType);
            }
            else if (param.HasDefaultValue)
            {
                parameterValues[i] = param.DefaultValue;
            }
            else if (param.IsOptional)
            {
                parameterValues[i] = GetDefaultValue(param.ParameterType);
            }
            else
            {
                throw new InvalidOperationException($"Required parameter '{param.Name}' not provided for function '{method.Name}'.");
            }
        }

        return parameterValues;
    }

    private static object ConvertArgument(object value, Type targetType)
    {
        if (value == null)
        {
            return GetDefaultValue(targetType);
        }

        var valueType = value.GetType();

        // Already the correct type
        if (targetType.IsAssignableFrom(valueType))
        {
            return value;
        }

        // Handle JsonElement
        if (value is JsonElement jsonElement)
        {
            return ConvertJsonElement(jsonElement, targetType);
        }

        // Handle numeric conversions
        if (IsNumericType(targetType))
        {
            return ConvertToNumeric(value, targetType);
        }

        // Handle string conversion
        if (targetType == typeof(string))
        {
            return value.ToString();
        }

        // Try general conversion
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            // Try JSON serialization/deserialization as last resort
            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize(json, targetType);
        }
    }

    private static object ConvertJsonElement(JsonElement element, Type targetType)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var stringValue = element.GetString();
                if (targetType == typeof(string))
                {
                    return stringValue;
                }
                if (IsNumericType(targetType) && stringValue != null)
                {
                    return ConvertToNumeric(stringValue, targetType);
                }
                break;

            case JsonValueKind.Number:
                if (targetType == typeof(int))
                {
                    return element.GetInt32();
                }
                if (targetType == typeof(long))
                {
                    return element.GetInt64();
                }
                if (targetType == typeof(double))
                {
                    return element.GetDouble();
                }
                if (targetType == typeof(float))
                {
                    return element.GetSingle();
                }
                if (targetType == typeof(decimal))
                {
                    return element.GetDecimal();
                }
                if (targetType == typeof(short))
                {
                    return element.GetInt16();
                }
                if (targetType == typeof(byte))
                {
                    return element.GetByte();
                }
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                if (targetType == typeof(bool))
                {
                    return element.GetBoolean();
                }
                break;
        }

        // Fall back to deserialize
        return JsonSerializer.Deserialize(element.GetRawText(), targetType);
    }

    private static object ConvertToNumeric(object value, Type targetType)
    {
        if (value is string strValue)
        {
            if (targetType == typeof(int) && int.TryParse(strValue, out int intResult))
            {
                return intResult;
            }
            if (targetType == typeof(long) && long.TryParse(strValue, out long longResult))
            {
                return longResult;
            }
            if (targetType == typeof(double) && double.TryParse(strValue, out double doubleResult))
            {
                return doubleResult;
            }
            if (targetType == typeof(float) && float.TryParse(strValue, out float floatResult))
            {
                return floatResult;
            }
            if (targetType == typeof(decimal) && decimal.TryParse(strValue, out decimal decimalResult))
            {
                return decimalResult;
            }
            if (targetType == typeof(short) && short.TryParse(strValue, out short shortResult))
            {
                return shortResult;
            }
            if (targetType == typeof(byte) && byte.TryParse(strValue, out byte byteResult))
            {
                return byteResult;
            }
        }

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot convert value '{value}' of type {value.GetType()} to {targetType}", ex);
        }
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(double) ||
               type == typeof(float) || type == typeof(decimal) || type == typeof(short) ||
               type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) ||
               type == typeof(ushort) || type == typeof(sbyte);
    }

    private static object GetDefaultValue(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        return null;
    }

    private static string ConvertResultToString(object result)
    {
        if (result == null)
        {
            return string.Empty;
        }

        if (result is string strResult)
        {
            return strResult;
        }

        // Serialize complex objects to JSON
        return JsonSerializer.Serialize(result);
    }
}
