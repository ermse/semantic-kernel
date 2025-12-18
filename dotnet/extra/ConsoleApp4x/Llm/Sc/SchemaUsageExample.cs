using System;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace ConsoleApp4x.Llm.Sc;

public static class SchemaUsageExample
{
    public static void Run()
    {
        // -------------------------------------------------------------------------
        // CONCEPT:
        // The 'Schema' property in KernelParameterMetadata allows you to define 
        // the exact structure of the data that the Large Language Model (LLM) 
        // should provide for a function parameter.
        //
        // While Semantic Kernel can infer schemas from C# types (e.g., int, string, classes),
        // manually defining the Schema is powerful when:
        // 1. You want to enforce constraints (enums, ranges, regex patterns).
        // 2. The parameter is a dynamic type (like JsonElement or object) but needs a strict structure.
        // 3. You want to minimize token usage by providing a compact schema.
        // -------------------------------------------------------------------------

        Console.WriteLine("--- KernelParameterMetadata Schema Example ---\n");

        // Scenario: We have a function 'CreateWidget' that takes a configuration object.
        // We want to ensure the LLM picks a valid color and a size within a specific range.
        
        // 1. Define the JSON Schema explicitly
        string jsonSchemaString = """
        {
            "type": "object",
            "description": "Detailed configuration for the widget",
            "properties": {
                "material": {
                    "type": "string",
                    "description": "The material to build the widget from."
                },
                "color": {
                    "type": "string",
                    "enum": ["Red", "Green", "Blue", "Yellow"],
                    "description": "The finish color of the widget."
                },
                "size": {
                    "type": "integer",
                    "minimum": 1,
                    "maximum": 100,
                    "description": "The size in millimeters (1-100)."
                }
            },
            "required": ["material", "color", "size"]
        }
        """;

        // 2. Create the metadata for the parameter
        var parameterMetadata = new KernelParameterMetadata("widgetConfig")
        {
            Description = "Configuration settings for the new widget",
            IsRequired = true,
            // We set the ParameterType to object because the actual input might be a JSON string or JsonElement
            // that we deserialize manually, or a dynamic object.
            ParameterType = typeof(object), 
            
            // 3. Assign the parsed schema
            Schema = KernelJsonSchema.Parse(jsonSchemaString)
        };

        // 4. Simulate what happens when this is sent to the LLM.
        // The LLM will see this schema and know exactly how to format the 'widgetConfig' argument.
        
        Console.WriteLine($"Parameter Name: {parameterMetadata.Name}");
        Console.WriteLine($"Description:    {parameterMetadata.Description}");
        Console.WriteLine($"Is Required:    {parameterMetadata.IsRequired}");
        Console.WriteLine("Schema (as sent to LLM):");
        Console.WriteLine(parameterMetadata.Schema.ToString());

        // -------------------------------------------------------------------------
        // Example Output Validation
        // -------------------------------------------------------------------------
        // If the LLM returns: { "material": "Plastic", "color": "Red", "size": 50 } -> Valid
        // If the LLM returns: { "material": "Wood", "color": "Purple", "size": 150 } -> Invalid (Color not in enum, size > 100)
    }
}
