// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using MySkUtils;

namespace ConsoleApp4x;

internal static class KernelProvider
{
    internal static Kernel GetKernel(IServiceProvider sp)
    {
        var httpClient = LlmHttpClientProvider.GetHttpClient(
            TimeSpan.FromSeconds(180),
            3,
            TimeSpan.FromSeconds(5),
            "C:\\tmp\\SemanticKernelDebug\\log.txt");


        var kb = Kernel.CreateBuilder();

        //var kernel = CreateAzureAiKernel(sp, httpClient, kb);
        var kernel = CreateGeminiAiKernel(sp, httpClient, kb);
        return kernel;
    }

    private static Kernel CreateAzureAiKernel(IServiceProvider sp, System.Net.Http.HttpClient httpClient, IKernelBuilder kb)
    {
        var opts = sp.GetRequiredService<IOptions<AzureOpenAIConfig>>().Value;
        var kernel = kb
           .AddAzureOpenAIChatCompletion(
               deploymentName: opts.Deployment,
               endpoint: opts.Endpoint,
               apiKey: opts.ApiKey,
               modelId: opts.ModelId,
               httpClient: httpClient)
           .Build();
        Console.WriteLine("Kernel created successfully with Azure OpenAI configuration!");
        Console.WriteLine($"Deployment: {opts.Deployment}");
        Console.WriteLine($"Endpoint: {opts.Endpoint}");
        Console.WriteLine($"Model ID: {opts.ModelId ?? "Not specified"}");
        return kernel;
    }

    private static Kernel CreateGeminiAiKernel(IServiceProvider sp, System.Net.Http.HttpClient httpClient, IKernelBuilder kb)
    {
        var opts = sp.GetRequiredService<IOptions<GeminiAiConfig>>().Value;
        var kernel = kb
           .AddGoogleAIGeminiChatCompletion(
            opts.ModelId,
            opts.ApiKey,
            httpClient: httpClient)
           .Build();
        Console.WriteLine("Kernel created successfully with Gemini AO configuration!");
        Console.WriteLine($"Model ID: {opts.ModelId}");
        return kernel;
    }
}
