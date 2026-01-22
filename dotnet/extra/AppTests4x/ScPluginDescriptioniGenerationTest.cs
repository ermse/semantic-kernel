// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using ConsoleApp4x;
using DicomUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using MyPlugins;

namespace AppTests4x;

public class ScPluginDescriptioniGenerationTest
{
    /// <summary>
    /// Tests whether a plugin description can be generated using the kernel.
    /// </summary>
    [Fact]
    public void CanGeneratePluginDescriptionUsingKernel()
    {

        Kernel kernel = new Kernel();
        kernel.ImportPluginFromType<StaticTextPluginDemo>();
        kernel.ImportPluginFromType<DicomPlugin>();
        var metatadata = kernel.Plugins.GetFunctionsMetadata();

        var options = new JsonSerializerOptions() { WriteIndented = true };
        options.Converters.Add(new TypeJsonConverter());
        options.Converters.Add(new KernelFunctionMetadataJsonConverter());
        options.Converters.Add(new KernelParameterMetadataJsonConverter());

        var json = JsonSerializer.Serialize(metatadata, options);

        IList<KernelFunctionMetadata> deserialized = JsonSerializer.Deserialize<IList<KernelFunctionMetadata>>(json, options);

        Assert.Equal(3, deserialized.Count);
        Assert.Equal("StaticTextPluginDemo", deserialized[0].PluginName);
    }

    /// <summary>
    /// Verifies that multiple services registered with the same key can be resolved as an enumerable from the service
    /// provider.
    /// </summary>
    /// <remarks>This test ensures that when multiple services are registered as keyed services with the same
    /// key, they can be retrieved as an IEnumerable using GetKeyedServices. The test confirms that all expected service
    /// instances are present in the resolved collection.</remarks>
    [Fact]
    public void CanAddIEnumerableType()
    {
        IServiceCollection sc = new ServiceCollection();

        // Register StaticTextPluginDemo and DicomPlugin as keyed services with key `123`
        sc.AddKeyedTransient<StaticTextPluginDemo>(serviceKey: LlmFunction);
        sc.AddKeyedTransient<DicomPlugin>(serviceKey: LlmFunction);
        sc.AddTransient<StaticTextPluginDemo>();
        sc.AddTransient<DicomPlugin>();

        // Get list of types registered for the key directly from IServiceCollection
        List<Type> typesForKey = sc
            .Where(descriptor => descriptor.IsKeyedService && Equals(descriptor.ServiceKey, LlmFunction))
            .Select(descriptor => descriptor.KeyedImplementationType ?? descriptor.ServiceType)
            .ToList();

        // Verify the registered types
        Assert.Equal(2, typesForKey.Count);
        Assert.Contains(typeof(StaticTextPluginDemo), typesForKey);
        Assert.Contains(typeof(DicomPlugin), typesForKey);

        // Build ServiceProvider
        var sp = sc.BuildServiceProvider();
        var stpd = sp.GetRequiredService<StaticTextPluginDemo>();
        var dp = sp.GetRequiredService<DicomPlugin>();

        Kernel kernel = new Kernel(sp);
        foreach (Type t in typesForKey)
        {
            var plugin = KernelPluginFactory.CreateFromType(t, serviceProvider: sp);
            kernel.Plugins.Add(plugin);
        }
        IList<KernelFunctionMetadata> metadata = kernel.Plugins.GetFunctionsMetadata();
        Assert.Equal(3, metadata.Count);
        var options = new JsonSerializerOptions();
#if DEBUG
        options.WriteIndented = true;
#endif
        options.Converters.Add(new TypeJsonConverter());
        var json = JsonSerializer.Serialize(metadata, options);
    }

    /// <summary>
    /// Represents the unique identifier for the LLM function feature.
    /// </summary>
    public static Guid LlmFunction { get; } = new Guid("f2be71b7-665e-4e75-81c9-fd1912d4cac3");
}
