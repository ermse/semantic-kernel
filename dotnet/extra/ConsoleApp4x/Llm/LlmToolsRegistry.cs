// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleApp4x;

/// <summary>
/// Registry for LLM tools that manages service collection and tool types.
/// This class enables dynamic registration and execution of tool implementations.
/// </summary>
public class LlmToolsRegistry
{
    private readonly object _syncLock = new object();
    private IServiceProvider _serviceProvider;
    private LlmToolInvoker _toolInvoker;

    /// <summary>
    /// Gets the service collection for registering tool dependencies.
    /// </summary>
    public IServiceCollection ServiceCollection { get; } = new ServiceCollection();

    /// <summary>
    /// Gets the list of types designated as LLM tools.
    /// </summary>
    public List<Type> Tools { get; } = new List<Type>();

    /// <summary>
    /// Generates JSON descriptions for all registered tools.
    /// </summary>
    /// <returns>A JSON array string containing descriptions of all registered tools.</returns>
    public string DescribeTools()
    {
        var descriptions = new List<string>();

        foreach (var toolType in this.Tools)
        {
            var description = LlmPluginDescriber.CreatePluginDescriptionJson(toolType);
            descriptions.Add(description);
        }

        if (descriptions.Count == 0)
        {
            return "[]";
        }

        if (descriptions.Count == 1)
        {
            // If single tool returns single object, wrap in array; if array, return as-is
            var single = descriptions[0];
            if (single.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                return single;
            }
            return "[" + single + "]";
        }

        // Combine all descriptions into a single array
        var combined = new List<object>();
        foreach (var desc in descriptions)
        {
            var trimmed = desc.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                // It's an array, parse and add individual items
                var items = JsonSerializer.Deserialize<List<object>>(trimmed);
                if (items != null)
                {
                    combined.AddRange(items);
                }
            }
            else
            {
                // It's a single object
                var item = JsonSerializer.Deserialize<object>(trimmed);
                if (item != null)
                {
                    combined.Add(item);
                }
            }
        }

        return JsonSerializer.Serialize(combined);
    }

    /// <summary>
    /// Registers a tool type in both the service collection and tools list.
    /// </summary>
    /// <typeparam name="T">The tool type to register.</typeparam>
    public void RegisterTool<T>() where T : class
    {
        this.ServiceCollection.AddTransient<T>();
        this.Tools.Add(typeof(T));
    }

    /// <summary>
    /// Registers a tool type in both the service collection and tools list.
    /// </summary>
    /// <param name="toolType">The tool type to register.</param>
    public void RegisterTool(Type toolType)
    {
        this.ServiceCollection.AddTransient(toolType);
        this.Tools.Add(toolType);
    }

    /// <summary>
    /// Gets or builds the service provider from the service collection.
    /// The service provider is built only once and cached.
    /// </summary>
    /// <returns>The service provider instance.</returns>
    public IServiceProvider GetServiceProvider()
    {
        if (_serviceProvider == null)
        {
            lock (_syncLock)
            {
                if (_serviceProvider == null)
                {
                    _serviceProvider = this.ServiceCollection.BuildServiceProvider();
                }
            }
        }

        return _serviceProvider;
    }

    /// <summary>
    /// Gets the tool invoker instance for executing function calls.
    /// </summary>
    /// <returns>The tool invoker instance.</returns>
    public LlmToolInvoker GetToolInvoker()
    {
        if (_toolInvoker == null)
        {
            lock (_syncLock)
            {
                if (_toolInvoker == null)
                {
                    _toolInvoker = new LlmToolInvoker(this);
                }
            }
        }

        return _toolInvoker;
    }

    /// <summary>
    /// Executes a function based on the provided function call data.
    /// </summary>
    /// <param name="functionCallData">The function call data containing plugin name, function name, and arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the function execution as a string.</returns>
    public Task<string> ExecuteFunctionAsync(LlmToolCallInfo functionCallData, CancellationToken cancellationToken)
    {
        return this.GetToolInvoker().ExecuteFunctionAsync(functionCallData, cancellationToken);
    }
}
