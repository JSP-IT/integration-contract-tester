using System.Text.Json;
using IntegrationContractTester.Models;
using Xunit;

namespace IntegrationContractTester.Tests;

public sealed class ContractComparerTests
{
    [Fact]
    public void OpenApiComparisonSeparatesBreakingAndNonBreakingChanges()
    {
        var baseline = ContractReader.Read(FixturePath("openapi-v1.json"), "openapi");
        var candidate = ContractReader.Read(FixturePath("openapi-v2.json"), "openapi");

        var findings = ContractComparer.Compare(baseline, candidate);

        Assert.Contains(findings, finding =>
            finding.Code == "RESPONSE_PROPERTY_REMOVED"
            && finding.Severity == FindingSeverity.Breaking
            && finding.Location.EndsWith(".status", StringComparison.Ordinal));
        Assert.Contains(findings, finding =>
            finding.Code == "OPTIONAL_PROPERTY_ADDED"
            && finding.Severity == FindingSeverity.NonBreaking);
        Assert.DoesNotContain(findings, finding => finding.Code == "OPERATION_REMOVED");
    }

    [Fact]
    public void JsonSchemaComparisonDetectsNewRequiredRequestProperty()
    {
        var baselinePath = WriteTempFile("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""");
        var candidatePath = WriteTempFile("""{"type":"object","properties":{"name":{"type":"string"},"accountId":{"type":"integer"}},"required":["name","accountId"]}""");

        try
        {
            var baseline = ContractReader.Read(baselinePath, "json-schema");
            var candidate = ContractReader.Read(candidatePath, "json-schema");

            var finding = Assert.Single(
                ContractComparer.Compare(baseline, candidate),
                item => item.Code == "REQUIRED_PROPERTY_ADDED");
            Assert.Equal(FindingSeverity.Breaking, finding.Severity);
        }
        finally
        {
            File.Delete(baselinePath);
            File.Delete(candidatePath);
        }
    }

    [Fact]
    public void UnsupportedSchemaFeaturesAreReportedAsWarnings()
    {
        var baselinePath = WriteTempFile("""{"type":"string","format":"email"}""");
        var candidatePath = WriteTempFile("""{"type":"string","format":"uri"}""");

        try
        {
            var findings = ContractComparer.Compare(
                ContractReader.Read(baselinePath, "json-schema"),
                ContractReader.Read(candidatePath, "json-schema"));

            Assert.Contains(findings, finding =>
                finding.Code == "UNSUPPORTED_SCHEMA_FEATURE"
                && finding.Severity == FindingSeverity.Warning
                && finding.Message.Contains("format", StringComparison.Ordinal));
            Assert.Contains(findings, finding => finding.Code == "FORMAT_CHANGED");
        }
        finally
        {
            File.Delete(baselinePath);
            File.Delete(candidatePath);
        }
    }

    [Fact]
    public void InvalidSchemaTypeIsRejectedWithActionableInputError()
    {
        var path = WriteTempFile("""{"type":3}""");
        try
        {
            Assert.Throws<InvalidDataException>(() => ContractReader.Read(path, "json-schema"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OpenApiYamlIsAccepted()
    {
        var path = WriteTempFile("""
            openapi: 3.0.3
            info:
              title: Status API
              version: 1.0.0
            paths:
              /status:
                get:
                  responses:
                    "200":
                      description: OK
            """);

        try
        {
            var contract = ContractReader.Read(path, "openapi");
            Assert.Contains("GET /status", contract.Operations.Keys);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExternalOpenApiReferencesAreNotFetchedAndAreReported()
    {
        var path = WriteTempFile("""
            {
              "openapi": "3.0.3",
              "info": { "title": "External reference test", "version": "1.0.0" },
              "paths": {
                "/orders": {
                  "get": {
                    "responses": {
                      "200": {
                        "description": "Order",
                        "content": {
                          "application/json": {
                            "schema": { "$ref": "https://example.invalid/order.schema.json" }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        try
        {
            var contract = ContractReader.Read(path, "openapi");
            var findings = ContractComparer.Compare(contract, contract);

            Assert.Contains(findings, finding =>
                finding.Code == "UNSUPPORTED_SCHEMA_FEATURE"
                && finding.Message.Contains("unresolved schema reference", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingInputFileIsReportedAsAnInputError()
    {
        Assert.Throws<FileNotFoundException>(() =>
            ContractReader.Read(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), "json-schema"));
    }

    [Fact]
    public void MalformedJsonIsNotSilentlyAccepted()
    {
        var path = WriteTempFile("""{"type":""");
        try
        {
            Assert.ThrowsAny<JsonException>(() => ContractReader.Read(path, "json-schema"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "examples", name);

    private static string WriteTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }
}
