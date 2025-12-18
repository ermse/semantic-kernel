// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace ConsoleApp4x;

public class KernelParameterMetadataJsonConverter : JsonConverter<KernelParameterMetadata>
{
    public override KernelParameterMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        string name = root.GetProperty("Name").GetString();
        var metadata = new KernelParameterMetadata(name);

        if (root.TryGetProperty("Description", out var description))
        {
            metadata.Description = description.GetString();
        }

        if (root.TryGetProperty("DefaultValue", out var defaultValue))
        {
            var val = JsonSerializer.Deserialize<object>(defaultValue.GetRawText(), options);
            if (val is JsonElement element)
            {
                metadata.DefaultValue = GetValueFromJsonElement(element);
            }
            else
            {
                metadata.DefaultValue = val;
            }
        }

        if (root.TryGetProperty("IsRequired", out var isRequired))
        {
            metadata.IsRequired = isRequired.GetBoolean();
        }

        if (root.TryGetProperty("ParameterType", out var parameterType))
        {
            metadata.ParameterType = JsonSerializer.Deserialize<Type>(parameterType.GetRawText(), options);
        }

        if (root.TryGetProperty("Schema", out var schema))
        {
            metadata.Schema = JsonSerializer.Deserialize<KernelJsonSchema>(schema.GetRawText(), options);
        }

        return metadata;
    }

    public override void Write(Utf8JsonWriter writer, KernelParameterMetadata value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Name", value.Name);
        writer.WriteString("Description", value.Description);

        writer.WritePropertyName("DefaultValue");
        JsonSerializer.Serialize(writer, value.DefaultValue, options);

        writer.WriteBoolean("IsRequired", value.IsRequired);

        // ParameterType is ignored in default serialization

        writer.WritePropertyName("Schema");
        JsonSerializer.Serialize(writer, value.Schema, options);

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
