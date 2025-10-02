using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ConsoleApp4x
{
    internal static class ChatHistoryDeserializer
    {
        internal static async ValueTask<ChatHistory> LoadChatHistoryFromJsonAsync(string filePath, CancellationToken cancel)
        {
            try
            {
                var fullPath = Path.GetFullPath(filePath);
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"Warning: Chat history file not found at {fullPath}. Creating empty chat history.");
                    return new ChatHistory();
                }

                // Use synchronous File.ReadAllText for .NET Framework 4.7.2 compatibility
                var jsonContent = File.ReadAllText(fullPath);
                using var document = JsonDocument.Parse(jsonContent);
                var chatHistory = new ChatHistory();
                // Parse the JSON array of messages
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var messageElement in document.RootElement.EnumerateArray())
                    {
                        var message = ParseChatMessage(messageElement);
                        if (message != null)
                        {
                            chatHistory.Add(message);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Warning: Expected JSON array format for chat history.");
                }

                return chatHistory;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading chat history: {ex.Message}. Creating empty chat history.");
                return new ChatHistory();
            }
        }

        private static ChatMessageContent ParseChatMessage(JsonElement messageElement)
        {
            try
            {
                // Extract role
                var role = AuthorRole.User; // default
                if (messageElement.TryGetProperty("role", out var roleProperty) &&
                    roleProperty.TryGetProperty("label", out var labelProperty))
                {
                    var roleString = labelProperty.GetString();
                    role = roleString.ToLowerInvariant() switch
                    {
                        "system" => AuthorRole.System,
                        "assistant" => AuthorRole.Assistant,
                        "user" => AuthorRole.User,
                        "tool" => AuthorRole.Tool,
                        _ => AuthorRole.User
                    };
                }

                // Extract content from items array
                var contentItems = new ChatMessageContentItemCollection();
                if (messageElement.TryGetProperty("items", out var itemsProperty) &&
                    itemsProperty.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsProperty.EnumerateArray())
                    {
                        if (item.TryGetProperty("$type", out var typeProperty))
                        {
                            var itemType = typeProperty.GetString();
                            if (itemType == "TextContent")
                            {
                                if (item.TryGetProperty("text", out var textProperty))
                                {
                                    var text = textProperty.GetString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        contentItems.Add(new TextContent(text));
                                    }
                                }
                            }
                            else if (itemType == "ImageContent")
                            {
                                var imageContent = CreateImageContent(item);
                                if (imageContent != null)
                                {
                                    contentItems.Add(imageContent);
                                }
                            }
                        }
                    }
                }

                // Create the chat message with items
                return new ChatMessageContent(role, contentItems, null, null, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to parse chat message: {ex.Message}");
                return null;
            }
        }

        private static ImageContent CreateImageContent(JsonElement imageItem)
        {
            try
            {
                // Try to extract URI first
                if (imageItem.TryGetProperty("uri", out var uriProperty))
                {
                    var uriString = uriProperty.GetString();
                    if (!string.IsNullOrEmpty(uriString) && Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                    {
                        return new ImageContent(uri);
                    }
                }

                // Try to extract data URI
                if (imageItem.TryGetProperty("dataUri", out var dataUriProperty))
                {
                    var dataUri = dataUriProperty.GetString();
                    if (!string.IsNullOrEmpty(dataUri))
                    {
                        return new ImageContent(dataUri);
                    }
                }

                // Try to extract binary data
                if (imageItem.TryGetProperty("data", out var dataProperty))
                {
                    var base64Data = dataProperty.GetString();
                    if (!string.IsNullOrEmpty(base64Data))
                    {
                        try
                        {
                            var imageBytes = Convert.FromBase64String(base64Data);
                            var mimeType = "image/jpeg"; // default

                            if (imageItem.TryGetProperty("mimeType", out var mimeTypeProperty))
                            {
                                var extractedMimeType = mimeTypeProperty.GetString();
                                if (!string.IsNullOrEmpty(extractedMimeType))
                                {
                                    mimeType = extractedMimeType;
                                }
                            }

                            return new ImageContent(imageBytes, mimeType);
                        }
                        catch (FormatException)
                        {
                            Console.WriteLine("Warning: Invalid base64 data in image content");
                        }
                    }
                }

                Console.WriteLine("Warning: Could not create ImageContent - no valid URI, data URI, or binary data found");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to create ImageContent: {ex.Message}");
                return null;
            }
        }
    }
}
