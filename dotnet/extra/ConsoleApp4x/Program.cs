// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

namespace ConsoleApp4x
{
    internal static class Program
    {

        private static async Task Main(string[] args)
        {
            try
            {


                using IHost host = HostComposer.CreateHost(args);
                IHostApplicationLifetime lf = host.Services
                    .GetRequiredService<IHostApplicationLifetime>();

                Kernel kernel = KernelProvider.GetKernel(host.Services);

                //await ChatDeserializerTest.DoTestAsync(
                //    "Resources/ChatHistoryDump-exam-11-video-tiled.json",
                //    azureOpenAIConfig,
                //    lf.ApplicationStopping);

                //await FunctionCallAutoTest.DoTestAsync(
                //    "Resources/cdf069b023ea4e33b5c78ac1eff45370_ChatHistoryDump.json",
                //    azureOpenAIConfig,
                //    lf.ApplicationStopping);

                //await FunctionCallManualTest.DoTestAsync(
                //   "Resources/cdf069b023ea4e33b5c78ac1eff45370_ChatHistoryDump.json",
                //   azureOpenAIConfig,
                //   lf.ApplicationStopping);

                //await FunctionCallManualRemoteTest.DoTestAsync(
                //   "Resources/cdf069b023ea4e33b5c78ac1eff45370_ChatHistoryDump.json",
                //   azureOpenAIConfig,
                //   lf.ApplicationStopping);

                await FunctionCallAutoRemoteTest.DoTestAsync(
                  "Resources/cdf069b023ea4e33b5c78ac1eff45370_ChatHistoryDump.json",
                  kernel,
                  lf.ApplicationStopping);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}
