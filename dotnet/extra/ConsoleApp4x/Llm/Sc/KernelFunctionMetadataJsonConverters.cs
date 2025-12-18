// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace ConsoleApp4x;

public class KernelFunctionMetadataJsonConverter : JsonConverter<KernelFunctionMetadata>
{
    public override KernelFunctionMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        string name = root.GetProperty("Name").GetString();
        var metadata = new KernelFunctionMetadata(name);

        if (root.TryGetProperty("PluginName", out var pluginName))
        {
            metadata.PluginName = pluginName.GetString();
        }

        if (root.TryGetProperty("Description", out var description))
        {
            metadata.Description = description.GetString();
        }

        if (root.TryGetProperty("Parameters", out var parameters))
        {
            metadata.Parameters = JsonSerializer.Deserialize<IReadOnlyList<KernelParameterMetadata>>(parameters.GetRawText(), options) ?? new List<KernelParameterMetadata>();
        }

        if (root.TryGetProperty("ReturnParameter", out var returnParameter))
        {
            metadata.ReturnParameter = JsonSerializer.Deserialize<KernelReturnParameterMetadata>(returnParameter.GetRawText(), options) ?? new KernelReturnParameterMetadata();
        }

        if (root.TryGetProperty("AdditionalProperties", out var additionalProperties))
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(additionalProperties.GetRawText(), options);
            if (dict != null)
            {
                var newDict = new Dictionary<string, object?>();
                foreach (var kvp in dict)
                {
                    if (kvp.Value is JsonElement element)
                    {
                        newDict[kvp.Key] = GetValueFromJsonElement(element);
                    }
                    else
                    {
                        newDict[kvp.Key] = kvp.Value;
                    }
                }
                metadata.AdditionalProperties = new ReadOnlyDictionary<string, object?>(newDict);
            }
        }

        return metadata;
    }

    public override void Write(Utf8JsonWriter writer, KernelFunctionMetadata value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Name", value.Name);
        writer.WriteString("PluginName", value.PluginName);
        writer.WriteString("Description", value.Description);

        writer.WritePropertyName("Parameters");
        JsonSerializer.Serialize(writer, value.Parameters, options);

        writer.WritePropertyName("ReturnParameter");
        JsonSerializer.Serialize(writer, value.ReturnParameter, options);

        writer.WritePropertyName("AdditionalProperties");
        JsonSerializer.Serialize(writer, value.AdditionalProperties, options);

        writer.WriteEndObject();
    }

    private object? GetValueFromJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt32(out int i)) return i;
                if (element.TryGetInt64(out long l)) return l;
                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            default:
                return element;
        }
    }
}
