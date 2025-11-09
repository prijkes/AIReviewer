using System.Reflection;
using System.Text.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Generation;
using Namotion.Reflection;

namespace AIReviewer.AI;

/// <summary>
/// Generates JSON schemas for AI response models to enable structured outputs.
/// Schemas are cached per type for performance.
/// </summary>
internal static class AiResponseSchemaGenerator
{
    private static readonly Dictionary<Type, BinaryData> _schemaCache = [];
    private static readonly object _lock = new();

    /// <summary>
    /// Generates a JSON schema for the specified type.
    /// The schema is generated once per type and cached for subsequent calls.
    /// Validates that all properties have the JsonRequired attribute as required by OpenAI.
    /// </summary>
    /// <typeparam name="T">The type to generate a schema for.</typeparam>
    /// <returns>A BinaryData containing the JSON schema.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a property is missing the JsonRequired attribute.</exception>
    public static BinaryData GenerateSchema<T>() where T : class
    {
        var type = typeof(T);
        
        // Validate that all properties have JsonRequired attribute
        ValidateAllPropertiesRequired(type);
        
        if (!_schemaCache.TryGetValue(type, out var cachedSchema))
        {
            lock (_lock)
            {
                if (!_schemaCache.TryGetValue(type, out cachedSchema))
                {
                    var settings = new SystemTextJsonSchemaGeneratorSettings
                    {
                        SchemaType = SchemaType.JsonSchema,
                        GenerateAbstractSchemas = false,
                        GenerateXmlObjects = false, // Disable XML metadata - not needed for OpenAI
                        AlwaysAllowAdditionalObjectProperties = false, // Ensure strict schema
                        SerializerOptions = new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                        },
                        SchemaProcessors =
                        {
                            new JsonRequiredSchemaProcessor()
                        }
                    };

                    var schema = JsonSchema.FromType<T>(settings);
                    
                    // Configure schema to be strict
                    schema.AllowAdditionalProperties = false;

                    // Clean up the schema to ensure OpenAI compatibility
                    CleanSchemaForOpenAI(schema);

                    string schemaJson = schema.ToJson();
                    cachedSchema = BinaryData.FromString(schemaJson);
                    
                    _schemaCache[type] = cachedSchema;
                }
            }
        }

        return cachedSchema;
    }

    /// <summary>
    /// Validates that all properties in the type and its nested types have the JsonRequired attribute.
    /// OpenAI's structured outputs require all properties to be marked as required.
    /// </summary>
    private static void ValidateAllPropertiesRequired(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var property in properties)
        {
            var hasJsonRequired = property.GetCustomAttribute<JsonRequiredAttribute>() != null;
            var hasRequiredModifier = property.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>() != null;
            
            if (!hasJsonRequired && !hasRequiredModifier)
            {
                throw new InvalidOperationException(
                    $"Property '{property.Name}' on type '{type.Name}' must have the [JsonRequired] attribute. " +
                    $"OpenAI's structured outputs require all properties to be marked as required.");
            }
            
            // Recursively validate nested types (but not primitives, enums, or collections)
            var propertyType = property.PropertyType;
            
            // Unwrap nullable types
            propertyType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            
            // Unwrap collection types
            if (propertyType.IsGenericType)
            {
                var genericDef = propertyType.GetGenericTypeDefinition();
                if (genericDef == typeof(IReadOnlyList<>) || 
                    genericDef == typeof(IList<>) || 
                    genericDef == typeof(List<>) ||
                    genericDef == typeof(IEnumerable<>) ||
                    genericDef == typeof(ICollection<>))
                {
                    propertyType = propertyType.GetGenericArguments()[0];
                }
            }
            
            // Only validate custom types (not primitives, enums, or system types)
            if (propertyType.IsClass && 
                !propertyType.IsPrimitive && 
                !propertyType.IsEnum && 
                propertyType != typeof(string) &&
                propertyType.Namespace != null &&
                !propertyType.Namespace.StartsWith("System"))
            {
                ValidateAllPropertiesRequired(propertyType);
            }
        }
    }

    /// <summary>
    /// Cleans up the schema to ensure compatibility with OpenAI's structured outputs.
    /// Removes oneOf wrappers and XML metadata that NJsonSchema may add.
    /// </summary>
    private static void CleanSchemaForOpenAI(JsonSchema schema)
    {
        // Recursively clean up all definitions
        if (schema.Definitions != null)
        {
            foreach (var definition in schema.Definitions.Values)
            {
                CleanSchemaForOpenAI(definition);
            }
        }

        // Clean up properties
        if (schema.Properties != null)
        {
            foreach (var property in schema.Properties.Values)
            {
                // Remove XML metadata
                property.Xml = null;

                // If the property is an array, clean up its items
                if (property.Type == JsonObjectType.Array && property.Item != null)
                {
                    // Remove XML metadata from array items
                    property.Item.Xml = null;

                    // If items use oneOf with a single reference, unwrap it to a direct reference
                    if (property.Item.OneOf != null && property.Item.OneOf.Count == 1)
                    {
                        var singleSchema = property.Item.OneOf.First();
                        if (singleSchema.Reference != null)
                        {
                            // Replace the oneOf wrapper with a direct reference
                            property.Item = singleSchema;
                        }
                    }
                }

                // Recursively clean nested objects
                CleanSchemaForOpenAI(property);
            }
        }
    }

    /// <summary>
    /// Schema processor that ensures JsonRequired attributes are properly reflected in the schema's required array.
    /// This is necessary because NJsonSchema doesn't automatically populate the required array for System.Text.Json.Serialization.JsonRequiredAttribute.
    /// OpenAI's structured output requires that all required properties are explicitly listed in the 'required' array.
    /// </summary>
    private class JsonRequiredSchemaProcessor : ISchemaProcessor
    {
        public void Process(SchemaProcessorContext context)
        {
            // Process each property in the current type
            foreach (var property in context.ContextualType.Properties)
            {
                // Only process properties declared in this type (not inherited)
                if (property.PropertyInfo.DeclaringType != context.ContextualType.Type)
                {
                    continue;
                }

                // Check if property has JsonRequired attribute or C# required modifier
                var hasJsonRequired = property.GetAttribute<JsonRequiredAttribute>(inherit: false) != null;
                var hasRequiredModifier = property.GetAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>(inherit: false) != null;
                
                if (hasJsonRequired || hasRequiredModifier)
                {
                    // Get the JSON property name (respecting JsonPropertyName attribute)
                    var jsonPropertyNameAttr = property.GetAttribute<JsonPropertyNameAttribute>(inherit: false);
                    var jsonPropertyName = jsonPropertyNameAttr?.Name ?? property.Name;
                    
                    // Find the corresponding schema property by the JSON name
                    var schemaProperty = context.Schema.Properties
                        .FirstOrDefault(x => x.Key.Equals(jsonPropertyName, StringComparison.OrdinalIgnoreCase));
                    
                    if (schemaProperty.Value != null)
                    {
                        // Mark the property as required in the schema
                        schemaProperty.Value.IsRequired = true;
                    }
                }
            }
        }
    }
}
