// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;

namespace ConsoleApp4x
{
    // TODO: replace KernelArguments with our own classes,
    // so remote host do not have to refernece semantic kernel
    /// <summary>
    /// Helper class for deserializing function call data.
    /// </summary>
    public class FunctionCallData
    {
        public string Id { get; set; }
        public string PluginName { get; set; }
        public string FunctionName { get; set; }
        public KernelArguments Arguments { get; set; }
    }
}
