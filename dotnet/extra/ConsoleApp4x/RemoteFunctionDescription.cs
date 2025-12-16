// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace ConsoleApp4x;

public class RemoteFunctionDescription
{
    public string PluginName { get; set; }
    public string FunctionName { get; set; }
    public string Description { get; set; }
    public List<RemoteParameterDescription> Parameters { get; set; } = new List<RemoteParameterDescription>();
}
