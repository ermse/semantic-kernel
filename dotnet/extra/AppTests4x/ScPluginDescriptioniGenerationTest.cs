// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ConsoleApp4x;
using ConsoleApp4x.Llm;
using DicomUtils;
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


        var json = JsonSerializer.Serialize(metatadata, options);
        options.Converters.Add(new KernelFunctionMetadataJsonConverter());
        options.Converters.Add(new KernelParameterMetadataJsonConverter());
        IList<KernelFunctionMetadata> deserialized = JsonSerializer.Deserialize<IList<KernelFunctionMetadata>>(json, options);

        Assert.Equal(3, deserialized.Count);
        Assert.Equal("StaticTextPluginDemo", deserialized[0].PluginName);
    }
}
