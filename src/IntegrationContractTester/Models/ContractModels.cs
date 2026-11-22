namespace IntegrationContractTester.Models;

internal sealed class ApiContract
{
    /// <summary>
    /// Operations keyed by a normalized identity (method plus path template with path parameter names removed),
    /// so that renaming a path parameter does not look like a removed operation.
    /// </summary>
    public Dictionary<string, OperationContract> Operations { get; } = new(StringComparer.Ordinal);
    public List<string> Warnings { get; } = [];
}

internal sealed class OperationContract
{
    public required string DisplayName { get; init; }

    /// <summary>
    /// Parameters keyed by a normalized identity: path parameters by template position, header parameters
    /// case-insensitively, other parameters by location and name.
    /// </summary>
    public Dictionary<string, ParameterContract> Parameters { get; } = new(StringComparer.Ordinal);
    public SchemaContract? RequestBody { get; set; }
    public bool RequestBodyRequired { get; set; }
    public Dictionary<string, SchemaContract?> Responses { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed record ParameterContract(string DisplayName, bool Required, SchemaContract? Schema, string? Serialization);

internal sealed class SchemaContract
{
    public HashSet<string> Types { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SchemaContract> Properties { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Required { get; } = new(StringComparer.Ordinal);
    public HashSet<string>? EnumValues { get; set; }
    public SchemaContract? Items { get; set; }
    public bool? AdditionalPropertiesAllowed { get; set; }
    public string? Format { get; set; }
    public List<string> Unsupported { get; } = [];
}
