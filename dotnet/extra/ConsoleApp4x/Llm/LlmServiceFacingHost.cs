// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace ConsoleApp4x;

public class LlmServiceFacingHost
{
    public static IList<KernelFunctionMetadata> GetFunctionMetadata(string json)
    {
        var options = new JsonSerializerOptions(){ WriteIndented = true };
        options.Converters.Add(new TypeJsonConverter());
        options.Converters.Add(new KernelFunctionMetadataJsonConverter());
        options.Converters.Add(new KernelParameterMetadataJsonConverter());
        IList<KernelFunctionMetadata> deserialized = JsonSerializer.Deserialize<IList<KernelFunctionMetadata>>(json, options);
        return deserialized;
    }
}
