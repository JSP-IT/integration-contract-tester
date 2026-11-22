namespace IntegrationContractTester.Models;

internal sealed class ApiContract
{
    public Dictionary<string, OperationContract> Operations { get; } = new(StringComparer.Ordinal);
    public List<string> Warnings { get; } = [];
}

internal sealed class OperationContract
{
    public Dictionary<string, ParameterContract> Parameters { get; } = new(StringComparer.Ordinal);
    public SchemaContract? RequestBody { get; set; }
    public bool RequestBodyRequired { get; set; }
    public Dictionary<string, SchemaContract?> Responses { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed record ParameterContract(bool Required, SchemaContract? Schema, string? Serialization);

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
