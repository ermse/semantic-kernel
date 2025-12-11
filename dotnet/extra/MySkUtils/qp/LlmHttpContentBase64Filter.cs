using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MySkUtils
{
    internal static class LlmHttpContentBase64Filter
    {
        /// <summary>
        /// Filters out base64 encoded binary content for log's readability
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string FilterOutBase64Content(this string input)
        {
            var root = JsonNode.Parse(input);
            FilterJsonNode(root);
            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private static void FilterJsonNode(JsonNode node)
        {
            if (node == null)
                return;

            if (node is JsonObject obj)
            {
                // Check for Gemini inlineData pattern
                if (obj.ContainsKey("inlineData") && obj["inlineData"] is JsonObject inlineData)
                {
                    if (inlineData.ContainsKey("data") && inlineData["data"] is JsonValue dataValue)
                    {
                        var dataStr = dataValue.GetValue<string>();
                        if (!string.IsNullOrEmpty(dataStr) && (dataStr.Length > 50))
                        {
                            inlineData["data"] = JsonValue.Create("... actual base64 encoded binary content removed for brevity");
                            return;
                        }
                    }
                }

                // Check for ChatGPT image_url pattern
                if (obj.ContainsKey("image_url") && obj["image_url"] is JsonObject imageUrl)
                {
                    if (imageUrl.ContainsKey("url") && imageUrl["url"] is JsonValue urlValue)
                    {
                        var urlStr = urlValue.GetValue<string>();
                        bool dataUrl = false;
                        if (!string.IsNullOrEmpty(urlStr) && urlStr.StartsWith("data:"))
                        {
                            var semicolonIndex = urlStr.IndexOf(';');
                            var commaIndex = urlStr.IndexOf(',');
                            if (semicolonIndex > 5 && commaIndex > semicolonIndex)
                            {
                                dataUrl = true;
                                var prefix = urlStr.Substring(0, commaIndex + 1);
                                imageUrl["url"] = JsonValue.Create(prefix + "... actual base64 encoded binary content removed for brevity");
                            }
                        }
                        if (!dataUrl && urlStr.Length > 64)
                        {
                            imageUrl["url"] = JsonValue.Create("... actual base64 encoded binary content removed for brevity");
                        }
                    }
                }

                // Recursively process all child nodes
                foreach (var property in obj.ToList())
                {
                    FilterJsonNode(property.Value);
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    FilterJsonNode(item);
                }
            }
        }
    }
}
