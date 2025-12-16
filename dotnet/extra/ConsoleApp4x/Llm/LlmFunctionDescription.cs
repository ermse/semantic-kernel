// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace ConsoleApp4x;

public class LlmFunctionDescription
{
    public string PluginName { get; set; }
    public string FunctionName { get; set; }
    public string Description { get; set; }
    public List<LlmFunctionParameterDescription> Parameters { get; set; } = new List<LlmFunctionParameterDescription>();
}
