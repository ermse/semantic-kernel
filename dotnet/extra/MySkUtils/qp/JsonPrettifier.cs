using System;
using System.Text.Json;

namespace MySkUtils
{
    /// <summary>
    /// Provides utilities for prettifying JSON strings
    /// </summary>
    internal static class JsonPrettifier
    {
        /// <summary>
        /// Prettifies LLM request/response JSON by adding indentation
        /// </summary>
        /// <param name="jsonString">The JSON string to prettify</param>
        /// <returns>Prettified JSON string with indentation</returns>
        public static string PrettifyLlmRequestJson(string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return jsonString;
            }

            try
            {
                using var document = JsonDocument.Parse(jsonString);
                return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch (JsonException)
            {
                // If parsing fails, return the original string
                return jsonString;
            }
        }
    }
}
