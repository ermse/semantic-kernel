// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySkUtils;

namespace ConsoleApp4x;

internal static class HostComposer
{
    internal static IHost CreateHost(string[] args)
    {
        var hab = Host.CreateApplicationBuilder(args);

        hab.Services.AddOptions<AzureOpenAIConfig>()
            .BindConfiguration(AzureOpenAIConfig.ConfigSection)
            .ValidateDataAnnotations()
            .ValidateOnStart(); // works only if we call host.Start() 

        hab.Services.AddOptions<GeminiAiConfig>()
           .BindConfiguration(GeminiAiConfig.ConfigSection)
           .ValidateDataAnnotations()
           .ValidateOnStart(); // works only if we call host.Start() 

        var host = hab.Build();
        return host;
    }
}
