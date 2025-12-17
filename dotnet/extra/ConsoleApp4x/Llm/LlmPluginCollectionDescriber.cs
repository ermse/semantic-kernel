// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DicomUtils;
using Microsoft.SemanticKernel;
using MyPlugins;

namespace ConsoleApp4x.Llm;

public class LlmPluginCollectionDescriber
{
    public string DescribePlugins()
    {
        Kernel kernel = new Kernel();
        kernel.ImportPluginFromType<StaticTextPluginDemo>();
        kernel.ImportPluginFromType<DicomPlugin>();
        var metatadata = kernel.Plugins.GetFunctionsMetadata();
        
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TypeJsonConverter());

        var json = JsonSerializer.Serialize(metatadata, options);
        IList<KernelFunctionMetadata> deserialized = JsonSerializer.Deserialize<IList<KernelFunctionMetadata>>(json, options);
        return json;
    }
}
