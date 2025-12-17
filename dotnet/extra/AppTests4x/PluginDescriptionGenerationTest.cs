// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using ConsoleApp4x;
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
    //[Fact]
    public void CanGeneratePluginDescriptionUsingKernel()
    {
        Kernel kernel = new Kernel();
        kernel.ImportPluginFromType<StaticTextPluginDemo>();
        kernel.ImportPluginFromType<DicomPlugin>();
        var metatadata = kernel.Plugins.GetFunctionsMetadata();
        // I am recieving an exception here:
        // {"Serialization and deserialization of 'System.Type' instances is not supported. Path: $.ReturnParameter.ParameterType."}
        // could you please search how the code in SemanticKernel (exluding my Extra folder)
        // solves this issue when creating tool description for llm request
        var json = JsonSerializer.Serialize(metatadata);
        var deserializedMetadata = JsonSerializer.Deserialize<IList<KernelFunctionMetadata>>(json);
        Assert.Equal(2, deserializedMetadata.Count);
        Assert.Equal("StaticTextPluginDemo", deserializedMetadata[0].PluginName);
    }

}
