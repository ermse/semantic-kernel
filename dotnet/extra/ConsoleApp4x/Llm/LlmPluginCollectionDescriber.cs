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
        // I am recieving an exception here:
        // {"Serialization and deserialization of 'System.Type' instances is not supported. Path: $.ReturnParameter.ParameterType."}
        // Please create a solution which utilizes JsonSerializer extensibility to serialize and then deserialize
        // Type information as string containng full type name e.g. System.String
        var json = JsonSerializer.Serialize(metatadata);
        return json;
    }
}
