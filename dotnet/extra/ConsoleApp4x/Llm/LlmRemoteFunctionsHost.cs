// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DicomUtils;

namespace ConsoleApp4x;

/// <summary>
/// Hosts and executes remote LLM function calls.
/// This class receives serialized function call information and routes it to the appropriate implementation.
/// Note: This class is designed to be independent of Semantic Kernel.
/// </summary>
public class LlmRemoteFunctionsHost
{
    public static readonly string DicomPluginDescriptionJson = LlmPluginDescriber.CreatePluginDescriptionJson(typeof(DicomPlugin));

    private static readonly JsonSerializerOptions CiJsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LlmToolsRegistry _toolsRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmRemoteFunctionsHost"/> class.
    /// Registers default tools including DicomPlugin.
    /// </summary>
    public LlmRemoteFunctionsHost()
    {
        _toolsRegistry = new LlmToolsRegistry();

        // Register DicomPlugin in the registry
        _toolsRegistry.RegisterTool<DicomPlugin>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmRemoteFunctionsHost"/> class
    /// with a custom tools registry.
    /// </summary>
    /// <param name="toolsRegistry">The tools registry to use.</param>
    public LlmRemoteFunctionsHost(LlmToolsRegistry toolsRegistry)
    {
        _toolsRegistry = toolsRegistry ?? throw new ArgumentNullException(nameof(toolsRegistry));
    }

    /// <summary>
    /// Gets the tools registry used by this host.
    /// </summary>
    public LlmToolsRegistry ToolsRegistry => _toolsRegistry;

    /// <summary>
    /// Gets the JSON descriptions of all registered tools.
    /// This can be used to pass tool calling information to Semantic Kernel for LLM requests.
    /// </summary>
    /// <returns>A JSON string describing all registered tools.</returns>
    public string DescribeTools()
    {
        return _toolsRegistry.DescribeTools();
    }

    /// <summary>
    /// Executes a function remotely based on serialized function call information.
    /// </summary>
    /// <param name="functionCallJson">JSON containing the plugin name, function name, and arguments.</param>
    /// <param name="cancel">Cancellation token.</param>
    /// <returns>The result of the remote function execution as a string.</returns>
    public async Task<string> ExecuteFunctionAsync(string functionCallJson, CancellationToken cancel)
    {
        var functionCallData = JsonSerializer.Deserialize<FunctionCallData>(functionCallJson, CiJsonOptions);

        if (functionCallData == null)
        {
            throw new InvalidOperationException("Failed to deserialize function call JSON");
        }

        Console.WriteLine($"Remote execution - Plugin: {functionCallData.PluginName}, Function: {functionCallData.FunctionName}");

        // Delegate execution to the tools registry
        return await _toolsRegistry.ExecuteFunctionAsync(functionCallData, cancel);
    }
}
