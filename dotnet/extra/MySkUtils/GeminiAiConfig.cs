using System.ComponentModel.DataAnnotations;

namespace MySkUtils
{
    /// <summary>
    /// Configuration settings for Azure OpenAI connection
    /// </summary>
    public class GeminiAiConfig
    {
        /// <summary>
        /// Config Section
        /// </summary>
        public static string ConfigSection { get; } = "GeminiAiConfig";

        /// <summary>
        /// Gets or sets the model ID
        /// </summary>
        [Required]
        public string? ModelId { get; set; }

        /// <summary>
        /// Gets or sets the API key for authentication
        /// </summary>
        [Required]
        public string? ApiKey { get; set; }
    }
}
