using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4x
{
    /// <summary>
    /// Configuration settings for Azure OpenAI connection
    /// </summary>
    public class AzureOpenAIConfig
    {
        /// <summary>
        /// Gets or sets the deployment name for the Azure OpenAI model
        /// </summary>
        public string Deployment { get; set; }

        /// <summary>
        /// Gets or sets the endpoint URL for the Azure OpenAI service
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the model ID
        /// </summary>
        public string ModelId { get; set; }

        /// <summary>
        /// Gets or sets the API key for authentication
        /// </summary>
        public string ApiKey { get; set; }
    }
}
