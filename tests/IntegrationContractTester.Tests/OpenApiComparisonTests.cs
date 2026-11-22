using IntegrationContractTester.Models;
using Xunit;

namespace IntegrationContractTester.Tests;

public sealed class OpenApiComparisonTests
{
    private const string IdParameter = """[{"name":"id","in":"path","required":true,"schema":{"type":"string"}}]""";

    [Fact]
    public void ResponseEnumValueAddedIsBreaking()
    {
        var findings = Compare(
            Document(responseSchema: StatusEnum("\"open\",\"closed\"")),
            Document(responseSchema: StatusEnum("\"open\",\"closed\",\"cancelled\"")));

        var finding = Assert.Single(findings);
        Assert.Equal("RESPONSE_ENUM_VALUE_ADDED", finding.Code);
        Assert.Equal(FindingSeverity.Breaking, finding.Severity);
        Assert.EndsWith("response 200.status", finding.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponseEnumValueRemovedIsNonBreaking()
    {
        var findings = Compare(
            Document(responseSchema: StatusEnum("\"open\",\"closed\"")),
            Document(responseSchema: StatusEnum("\"open\"")));

        var finding = Assert.Single(findings);
        Assert.Equal("RESPONSE_ENUM_VALUE_REMOVED", finding.Code);
        Assert.Equal(FindingSeverity.NonBreaking, finding.Severity);
    }

    [Fact]
    public void RequestEnumValueRemovedIsBreaking()
    {
        var findings = Compare(
            Document(requestSchema: StatusEnum("\"open\",\"closed\"")),
            Document(requestSchema: StatusEnum("\"open\"")));

        var finding = Assert.Single(findings);
        Assert.Equal("ENUM_VALUE_REMOVED", finding.Code);
        Assert.Equal(FindingSeverity.Breaking, finding.Severity);
    }

    [Fact]
    public void EnumValuesAreComparedByValue()
    {
        var baseline = Document(responseSchema: """{"type":"integer","enum":[1,2]}""");

        Assert.Empty(Compare(baseline, Document(responseSchema: """{"type":"integer","enum":[2,1]}""")));
        Assert.Contains(
            Compare(baseline, Document(responseSchema: """{"type":"integer","enum":[1,2,3]}""")),
            finding => finding.Code == "RESPONSE_ENUM_VALUE_ADDED");
    }

    [Fact]
    public void RenamingPathParameterIsNotAChange()
    {
        var findings = Compare(
            Document(path: "/orders/{id}", parameters: IdParameter),
            Document(
                path: "/orders/{orderId}",
                parameters: """[{"name":"orderId","in":"path","required":true,"schema":{"type":"string"}}]"""));

        Assert.Empty(findings);
    }

    [Fact]
    public void ChangingPathParameterTypeIsStillDetectedAfterRename()
    {
        var findings = Compare(
            Document(path: "/orders/{id}", parameters: IdParameter),
            Document(
                path: "/orders/{orderId}",
                parameters: """[{"name":"orderId","in":"path","required":true,"schema":{"type":"integer"}}]"""));

        var finding = Assert.Single(findings);
        Assert.Equal("SCHEMA_TYPE_RESTRICTED", finding.Code);
        Assert.Contains("POST /orders/{orderId} parameter Path:orderId", finding.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderParameterNameIsCaseInsensitive()
    {
        var findings = Compare(
            Document(parameters: """[{"name":"X-Trace-Id","in":"header","schema":{"type":"string"}}]"""),
            Document(parameters: """[{"name":"x-trace-id","in":"header","schema":{"type":"string"}}]"""));

        Assert.Empty(findings);
    }

    [Fact]
    public void RequestPropertyBecomingRequiredIsBreaking()
    {
        var findings = Compare(
            Document(requestSchema: """{"type":"object","properties":{"note":{"type":"string"}}}"""),
            Document(requestSchema: """{"type":"object","properties":{"note":{"type":"string"}},"required":["note"]}"""));

        var finding = Assert.Single(findings);
        Assert.Equal("PROPERTY_NOW_REQUIRED", finding.Code);
        Assert.Equal(FindingSeverity.Breaking, finding.Severity);
    }

    [Fact]
    public void ResponsePropertyNoLongerRequiredIsBreaking()
    {
        var findings = Compare(
            Document(responseSchema: """{"type":"object","properties":{"id":{"type":"string"}},"required":["id"]}"""),
            Document(responseSchema: """{"type":"object","properties":{"id":{"type":"string"}}}"""));

        var finding = Assert.Single(findings);
        Assert.Equal("RESPONSE_PROPERTY_NO_LONGER_REQUIRED", finding.Code);
        Assert.Equal(FindingSeverity.Breaking, finding.Severity);
    }

    [Fact]
    public void NullableRequestIsCompatibleButNullableResponseIsBreaking()
    {
        var request = Compare(
            Document(requestSchema: """{"type":"string"}"""),
            Document(requestSchema: """{"type":"string","nullable":true}"""));
        var response = Compare(
            Document(responseSchema: """{"type":"string"}"""),
            Document(responseSchema: """{"type":"string","nullable":true}"""));

        Assert.Equal(FindingSeverity.NonBreaking, Assert.Single(request, item => item.Code == "SCHEMA_TYPE_WIDENED").Severity);
        Assert.Equal(FindingSeverity.Breaking, Assert.Single(response, item => item.Code == "SCHEMA_TYPE_RESTRICTED").Severity);
    }

    [Fact]
    public void RemovedOperationIsBreaking()
    {
        var findings = Compare(Document(path: "/orders"), Document(path: "/purchases"));

        Assert.Contains(findings, finding =>
            finding.Code == "OPERATION_REMOVED"
            && finding.Severity == FindingSeverity.Breaking
            && finding.Location == "POST /orders");
        Assert.Contains(findings, finding =>
            finding.Code == "OPERATION_ADDED" && finding.Location == "POST /purchases");
    }

    private static string StatusEnum(string values) =>
        """{"type":"object","properties":{"status":{"type":"string","enum":[""" + values + "]}}}";

    private static string Document(
        string path = "/orders/{id}",
        string? parameters = null,
        string? requestSchema = null,
        string responseSchema = """{"type":"object"}""")
    {
        parameters ??= path.Contains("{id}", StringComparison.Ordinal) ? IdParameter : "[]";
        var requestBody = requestSchema is null
            ? string.Empty
            : """ "requestBody": {"required": true, "content": {"application/json": {"schema": """ + requestSchema + "}}},";

        return $$"""
            {
              "openapi": "3.0.3",
              "info": { "title": "Test", "version": "1.0.0" },
              "paths": {
                "{{path}}": {
                  "post": {
                    "parameters": {{parameters}},
                    {{requestBody}}
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": { "application/json": { "schema": {{responseSchema}} } }
                      }
                    }
                  }
                }
              }
            }
            """;
    }

    private static IReadOnlyList<Finding> Compare(string baselineDocument, string candidateDocument)
    {
        using var files = new TempFiles();
        return ContractComparer.Compare(
            ContractReader.Read(files.Write(baselineDocument), "openapi"),
            ContractReader.Read(files.Write(candidateDocument), "openapi"));
    }
}
