// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using ConsoleApp4x;
using ConsoleApp4x.Llm;
using DicomUtils;
using Microsoft.SemanticKernel;
using MyPlugins;

namespace AppTests4x;

public class PluginDescriptionGenerationTest
{
    [Fact]
    public void CanGeneratePluginDescription()
    {
        // Act - Get the generated description
        var generatedJson = new LlmRemoteFunctionsHost().DescribeTools();

        // Assert - Verify it's valid JSON
        Assert.NotNull(generatedJson);
        Assert.NotEmpty(generatedJson);

        // Parse and verify structure
        var arr = JsonSerializer.Deserialize<IEnumerable<LlmFunctionDescription>>(generatedJson);
        var functionDesc = arr.First();

        Assert.NotNull(functionDesc);
        Assert.Equal("DicomPlugin", functionDesc.PluginName);
        Assert.Equal("RegionsJson", functionDesc.FunctionName);
        Assert.Equal("Returns string representing json containing [(0018,6011) Sequence of Ultrasound Regions] extracted from dicom file.", functionDesc.Description);

        // Verify parameters
        Assert.NotNull(functionDesc.Parameters);
        Assert.Single(functionDesc.Parameters);

        var parameter = functionDesc.Parameters[0];
        Assert.Equal("dicomFileId", parameter.Name);
        Assert.Equal("Id of the dicom file.", parameter.Description);
        Assert.Equal("integer", parameter.Type);
        Assert.True(parameter.IsRequired);
    }

    [Fact]
    public void GeneratedJsonCanBeDeserialized()
    {
        // Arrange
        var generatedJson = new LlmRemoteFunctionsHost().DescribeTools();

        // Act - Parse the JSON
        var jsonDoc = JsonDocument.Parse(generatedJson);

        // Assert - Verify it's a valid JSON document with an array
        Assert.NotNull(jsonDoc);
        Assert.NotNull(jsonDoc.RootElement);

        // Verify root is an array
        Assert.Equal(JsonValueKind.Array, jsonDoc.RootElement.ValueKind);
        Assert.True(jsonDoc.RootElement.GetArrayLength() > 0);

        // Verify first element has expected properties
        var firstElement = jsonDoc.RootElement[0];
        Assert.True(firstElement.TryGetProperty("PluginName", out _));
        Assert.True(firstElement.TryGetProperty("FunctionName", out _));
        Assert.True(firstElement.TryGetProperty("Description", out _));
        Assert.True(firstElement.TryGetProperty("Parameters", out var parameters));

        // Verify parameters is an array
        Assert.Equal(JsonValueKind.Array, parameters.ValueKind);
        Assert.Equal(1, parameters.GetArrayLength());
    }

    /// <summary>
    /// Tests whether a plugin description can be generated using the kernel.
    /// </summary>
    [Fact]
    public void CanGeneratePluginDescriptionUsingKernel()
    {
       
        Kernel kernel = new Kernel();
        kernel.ImportPluginFromType<StaticTextPluginDemo>();
        kernel.ImportPluginFromType<DicomPlugin>();
        var metatadata = kernel.Plugins.GetFunctionsMetadata();

        var options = new JsonSerializerOptions() { WriteIndented = true };
        options.Converters.Add(new TypeJsonConverter());

        var json = JsonSerializer.Serialize(metatadata, options);

        /* TODO:
         I am recieving an exception here:
         Message=Deserialization of types without a parameterless constructor, a singular parameterized constructor, or a parameterized constructor annotated with 'JsonConstructorAttribute' is not supported. Type 'Microsoft.SemanticKernel.KernelFunctionMetadata'
         apparently JsonSerializer is unable to instantiate KernelFunctionMetadata.
         Please create a Converter for it.

        this how the json looks like Name is Name of the function should be used when calling KernelFunctionMetadata constructor:
        [
          {
            "Name": "Uppercase",
            "PluginName": "StaticTextPluginDemo",
            "Description": "Change all string chars to uppercase",
            "Parameters": [
              {
                "Name": "input",
                "Description": "Text to uppercase",
                "DefaultValue": null,
                "IsRequired": true,
                "Schema": {
                  "description": "Text to uppercase",
                  "type": "string"
                }
              }
            ],
            "ReturnParameter": {
              "Description": "",
              "ParameterType": "System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
              "Schema": {
                "type": "string"
              }
            },
            "AdditionalProperties": {}
          },
          {
            "Name": "AppendDay",
            "PluginName": "StaticTextPluginDemo",
            "Description": "Append the day variable",
            "Parameters": [
              {
                "Name": "input",
                "Description": "Text to append to",
                "DefaultValue": null,
                "IsRequired": true,
                "Schema": {
                  "description": "Text to append to",
                  "type": "string"
                }
              },
              {
                "Name": "day",
                "Description": "Value of the day to append",
                "DefaultValue": null,
                "IsRequired": true,
                "Schema": {
                  "description": "Value of the day to append",
                  "type": "string"
                }
              }
            ],
            "ReturnParameter": {
              "Description": "",
              "ParameterType": "System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
              "Schema": {
                "type": "string"
              }
            },
            "AdditionalProperties": {}
          },
          {
            "Name": "RegionsJson",
            "PluginName": "DicomPlugin",
            "Description": "Returns string representing json containing [(0018,6011) Sequence of Ultrasound Regions] extracted from dicom file.",
            "Parameters": [
              {
                "Name": "dicomFileId",
                "Description": "Id of the dicom file.",
                "DefaultValue": null,
                "IsRequired": true,
                "Schema": {
                  "description": "Id of the dicom file.",
                  "type": "integer"
                }
              }
            ],
            "ReturnParameter": {
              "Description": "",
              "ParameterType": "System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
              "Schema": {
                "type": "string"
              }
            },
            "AdditionalProperties": {}
          }
        ]
         */
        IList<KernelFunctionMetadata> deserialized = JsonSerializer.Deserialize<IList<KernelFunctionMetadata>>(json, options);

          Assert.Equal(2, deserialized.Count);
        Assert.Equal("StaticTextPluginDemo", deserialized[0].PluginName);
    }

}
