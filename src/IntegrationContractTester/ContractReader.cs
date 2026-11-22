using System.Text.Json;
using IntegrationContractTester.Models;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace IntegrationContractTester;

internal static class ContractReader
{
    private static readonly HashSet<string> KnownJsonSchemaKeywords = new(StringComparer.Ordinal)
    {
        "$schema", "$id", "$ref", "$defs", "definitions", "$anchor", "$dynamicRef", "$dynamicAnchor", "$comment",
        "type", "title", "description", "default", "examples", "readOnly", "writeOnly", "deprecated", "format",
        "enum", "const", "multipleOf", "maximum", "exclusiveMaximum", "minimum", "exclusiveMinimum", "maxLength",
        "minLength", "pattern", "maxItems", "minItems", "uniqueItems", "contains", "maxProperties", "minProperties",
        "required", "properties", "patternProperties", "additionalProperties", "propertyNames", "if", "then", "else",
        "allOf", "anyOf", "oneOf", "not", "dependentRequired", "dependentSchemas", "unevaluatedItems",
        "unevaluatedProperties", "prefixItems", "contentEncoding", "contentMediaType", "contentSchema",
        "minContains", "maxContains"
    };

    public static ApiContract Read(string path, string format)
    {
        return format switch
        {
            "openapi" => ReadOpenApi(path),
            "json-schema" => ReadJsonSchema(path),
            _ => throw new ArgumentException($"Unsupported format '{format}'. Use 'openapi' or 'json-schema'.")
        };
    }

    private static ApiContract ReadOpenApi(string path)
    {
        using var stream = File.OpenRead(path);
        var settings = new OpenApiReaderSettings
        {
            LoadExternalRefs = false,
            ReferenceResolution = ReferenceResolutionSetting.ResolveLocalReferences
        };
        var reader = new OpenApiStreamReader(settings);
        var document = reader.Read(stream, out var diagnostic);
        if (diagnostic.Errors.Count > 0)
        {
            throw new InvalidDataException($"The OpenAPI document contains {diagnostic.Errors.Count} parsing error(s).");
        }

        var contract = new ApiContract();
        if (diagnostic.Warnings.Count > 0)
        {
            contract.Warnings.Add($"OpenAPI reader reported {diagnostic.Warnings.Count} warning(s); some document features may not be compared.");
        }

        if (document.SecurityRequirements.Count > 0
            || (document.Components is not null && document.Components.SecuritySchemes.Count > 0))
        {
            contract.Warnings.Add("OpenAPI security requirements and authentication schemes are not compared.");
        }

        foreach (var (pathName, pathItem) in document.Paths)
        {
            foreach (var (method, operation) in pathItem.Operations)
            {
                var key = $"{method.ToString().ToUpperInvariant()} {pathName}";
                var item = new OperationContract();
                if (operation.Security.Count > 0)
                {
                    contract.Warnings.Add($"{key} declares security requirements; authentication compatibility is not compared.");
                }

                foreach (var parameter in pathItem.Parameters.Concat(operation.Parameters)
                    .GroupBy(parameter => $"{parameter.In}:{parameter.Name}", StringComparer.Ordinal)
                    .Select(group => group.Last()))
                {
                    item.Parameters[$"{parameter.In}:{parameter.Name}"] =
                        new ParameterContract(
                            parameter.Required,
                            ConvertOpenApiSchema(parameter.Schema, new HashSet<OpenApiSchema>()),
                            $"{parameter.Style}|{parameter.Explode}|{parameter.AllowReserved}");
                }

                if (operation.RequestBody is not null)
                {
                    item.RequestBodyRequired = operation.RequestBody.Required;
                    item.RequestBody = ReadOpenApiContent(operation.RequestBody.Content, contract, $"{key} request");
                    item.RequestBody ??= UnsupportedSchema("request body has no comparable JSON schema");
                }

                foreach (var (status, response) in operation.Responses)
                {
                    if (response.Headers.Count > 0)
                    {
                        contract.Warnings.Add($"{key} response {status} declares headers; response header compatibility is not compared.");
                    }

                    item.Responses[status] = ReadOpenApiContent(response.Content, contract, $"{key} response {status}");
                }

                contract.Operations[key] = item;
            }
        }

        return contract;
    }

    private static SchemaContract? ReadOpenApiContent(
        IDictionary<string, OpenApiMediaType> content,
        ApiContract contract,
        string location)
    {
        if (content.Count == 0)
        {
            return null;
        }

        var jsonMediaTypes = content
            .Where(item => item.Key.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                || item.Key.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (jsonMediaTypes.Length == 0)
        {
            contract.Warnings.Add($"{location} has no supported JSON media type; its body schema was not compared.");
            return UnsupportedSchema("non-JSON media type");
        }

        if (jsonMediaTypes.Length > 1)
        {
            contract.Warnings.Add($"{location} declares multiple JSON media types; only the first schema was compared.");
        }

        return ConvertOpenApiSchema(jsonMediaTypes[0].Value.Schema, new HashSet<OpenApiSchema>())
            ?? UnsupportedSchema("missing media type schema");
    }

    private static SchemaContract UnsupportedSchema(string reason) =>
        new() { Unsupported = { reason } };

    private static SchemaContract? ConvertOpenApiSchema(OpenApiSchema? source, HashSet<OpenApiSchema> active)
    {
        if (source is null)
        {
            return null;
        }

        if (!active.Add(source))
        {
            return new SchemaContract { Unsupported = { "recursive schema reference" } };
        }

        try
        {
            var result = new SchemaContract();
            if (source.UnresolvedReference)
            {
                result.Unsupported.Add("unresolved schema reference");
            }

            if (!string.IsNullOrWhiteSpace(source.Type))
            {
                result.Types.Add(source.Type);
            }

            result.Format = source.Format;
            if (source.Nullable)
            {
                result.Types.Add("null");
            }
            if (source.Enum.Count > 0)
            {
                result.EnumValues = source.Enum.Select(value => value.ToString() ?? string.Empty).ToHashSet(StringComparer.Ordinal);
            }

            foreach (var required in source.Required)
            {
                result.Required.Add(required);
            }

            foreach (var (name, property) in source.Properties)
            {
                var converted = ConvertOpenApiSchema(property, active);
                if (converted is not null)
                {
                    result.Properties[name] = converted;
                }
            }

            result.Items = ConvertOpenApiSchema(source.Items, active);
            result.AdditionalPropertiesAllowed = source.AdditionalPropertiesAllowed;
            if (source.AdditionalProperties is not null)
            {
                result.Unsupported.Add("schema-valued additionalProperties");
            }
            if (source.ReadOnly || source.WriteOnly || source.Discriminator is not null)
            {
                result.Unsupported.Add("readOnly/writeOnly/discriminator");
            }
            if (source.AllOf.Count > 0 || source.AnyOf.Count > 0 || source.OneOf.Count > 0 || source.Not is not null)
            {
                result.Unsupported.Add("composition keywords (allOf/anyOf/oneOf/not)");
            }
            if (source.Minimum is not null || source.Maximum is not null || source.ExclusiveMinimum == true || source.ExclusiveMaximum == true
                || source.MultipleOf is not null || source.MinLength is not null || source.MaxLength is not null
                || source.Pattern is not null || source.MinItems is not null || source.MaxItems is not null
                || source.UniqueItems == true || source.MinProperties is not null || source.MaxProperties is not null)
            {
                result.Unsupported.Add("numeric/string/array/object constraints");
            }

            return result;
        }
        finally
        {
            active.Remove(source);
        }
    }

    private static ApiContract ReadJsonSchema(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A JSON Schema document must have an object at its root.");
        }

        var contract = new ApiContract();
        var schema = ConvertJsonSchema(document.RootElement);
        contract.Operations["JSON SCHEMA /"] = new OperationContract { RequestBody = schema };
        return contract;
    }

    private static SchemaContract ConvertJsonSchema(JsonElement source)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Every JSON Schema node must be an object.");
        }

        var result = new SchemaContract();
        foreach (var property in source.EnumerateObject())
        {
            if (!KnownJsonSchemaKeywords.Contains(property.Name)
                && !property.Name.StartsWith("x-", StringComparison.Ordinal))
            {
                result.Unsupported.Add($"unknown keyword '{property.Name}'");
            }
        }

        if (source.TryGetProperty("type", out var type))
        {
            if (type.ValueKind == JsonValueKind.String)
            {
                result.Types.Add(type.GetString()!);
            }
            else if (type.ValueKind == JsonValueKind.Array)
            {
                foreach (var value in type.EnumerateArray())
                {
                    if (value.ValueKind != JsonValueKind.String)
                    {
                        throw new InvalidDataException("Schema 'type' arrays may contain only strings.");
                    }

                    result.Types.Add(value.GetString()!);
                }
            }
            else
            {
                throw new InvalidDataException("Schema 'type' must be a string or an array of strings.");
            }
        }

        if (source.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String)
        {
            result.Format = format.GetString();
        }

        if (source.TryGetProperty("enum", out var enumValues))
        {
            if (enumValues.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Schema 'enum' must be an array.");
            }

            result.EnumValues = enumValues.EnumerateArray()
                .Select(value => value.GetRawText())
                .ToHashSet(StringComparer.Ordinal);
        }

        if (source.TryGetProperty("required", out var required))
        {
            if (required.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Schema 'required' must be an array.");
            }

            foreach (var value in required.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("Schema 'required' entries must be strings.");
                }

                result.Required.Add(value.GetString()!);
            }
        }

        if (source.TryGetProperty("properties", out var properties))
        {
            if (properties.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Schema 'properties' must be an object.");
            }

            foreach (var property in properties.EnumerateObject())
            {
                result.Properties[property.Name] = ConvertJsonSchema(property.Value);
            }
        }

        if (source.TryGetProperty("items", out var items))
        {
            result.Items = ConvertJsonSchema(items);
        }

        if (source.TryGetProperty("additionalProperties", out var additionalProperties))
        {
            if (additionalProperties.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                result.AdditionalPropertiesAllowed = additionalProperties.GetBoolean();
            }
            else if (additionalProperties.ValueKind == JsonValueKind.Object)
            {
                result.Unsupported.Add("schema-valued additionalProperties");
            }
            else
            {
                throw new InvalidDataException("Schema 'additionalProperties' must be a boolean or an object schema.");
            }
        }

        foreach (var keyword in new[]
        {
            "$ref", "$dynamicRef", "allOf", "anyOf", "oneOf", "not", "patternProperties", "if", "then", "else",
            "const", "readOnly", "writeOnly", "propertyNames", "dependentRequired", "dependentSchemas",
            "minProperties", "maxProperties", "contentEncoding", "contentMediaType", "contentSchema",
            "unevaluatedItems", "unevaluatedProperties", "prefixItems", "minContains", "maxContains"
        })
        {
            if (source.TryGetProperty(keyword, out _))
            {
                result.Unsupported.Add(keyword);
            }
        }

        foreach (var keyword in new[]
        {
            "format", "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf",
            "minLength", "maxLength", "pattern", "minItems", "maxItems", "uniqueItems", "contains"
        })
        {
            if (source.TryGetProperty(keyword, out _))
            {
                result.Unsupported.Add(keyword);
            }
        }

        return result;
    }
}
