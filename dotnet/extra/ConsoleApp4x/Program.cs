using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MySkUtils;

namespace ConsoleApp4x
{
    internal static class Program
    {

        private static async Task Main(string[] args)
        {
            try
            {
                var hab = Host.CreateApplicationBuilder(args);

                var host = hab.Build();
                var conf = host.Services.GetRequiredService<IConfiguration>();
                var env = host.Services.GetRequiredService<IHostEnvironment>();
                var lf = host.Services.GetRequiredService<IHostApplicationLifetime>();
                // Build configuration from appsettings.json, environment variables, and user secrets
                //var configuration = new ConfigurationBuilder()
                //    .AddJsonFile("appsettings.json", optional: true)
                //    .AddEnvironmentVariables()
                //    .AddUserSecrets<AzureOpenAIConfig>(optional: false)
                //    .Build();

                // Read AzureOpenAI configuration
                var azureOpenAIConfig = new AzureOpenAIConfig();
                conf.GetSection("AzureOpenAIConfig").Bind(azureOpenAIConfig);

                // Validate configuration
                if (string.IsNullOrWhiteSpace(azureOpenAIConfig.ApiKey) ||
                    string.IsNullOrWhiteSpace(azureOpenAIConfig.Endpoint) ||
                    string.IsNullOrWhiteSpace(azureOpenAIConfig.Deployment) ||
                    string.IsNullOrWhiteSpace(azureOpenAIConfig.ModelId))
                {
                    throw new InvalidOperationException("AzureOpenAI configuration is missing. Please configure ApiKey, EndPoint, and DeploymentName in appsettings.json or user secrets.");
                }

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

                await FunctionCallManualRemoteTest.DoTestAsync(
                   "Resources/cdf069b023ea4e33b5c78ac1eff45370_ChatHistoryDump.json",
                   azureOpenAIConfig,
                   lf.ApplicationStopping);

            }
            catch
            {
                throw;
            }
        }
    }
}
