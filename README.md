# Integration Contract Tester

Integration Contract Tester is a local .NET command-line tool that compares API contracts before deployment. It highlights likely breaking changes separately from compatible additions and schema features it cannot safely evaluate, helping teams catch integration regressions during code review or CI.

## Requirements

- .NET SDK 10.0 or later
- Windows, Linux, or macOS

## Install

Install the published .NET tool:

```shell
dotnet tool install --global JspIt.IntegrationContractTester --version 0.1.0
```

Or build and run from a source checkout:

```shell
dotnet restore
dotnet build --configuration Release
dotnet run --project src/IntegrationContractTester -- compare examples/openapi-v1.json examples/openapi-v2.json
```

The NuGet tool package is intended for the v0.1.0 release. Until that package is published, use the source-checkout commands above.

## Compare API versions

```shell
ict compare baseline.yaml candidate.yaml
```

OpenAPI is the default format. JSON and YAML OpenAPI documents are supported. The report lists each finding as `BREAKING`, `NON-BREAKING`, or `WARNING`; the process exits with `1` when a confirmed breaking change is found, `0` when none is found, and `2` for invalid input or command usage.

Example:

```shell
ict compare examples/openapi-v1.json examples/openapi-v2.json
```

Expected report (shortened):

```text
[BREAKING] RESPONSE_PROPERTY_REMOVED at GET /orders/{id} response 200.status: A response property was removed.
[NON-BREAKING] OPTIONAL_PROPERTY_ADDED at POST /orders request body.note: An optional request property was added.

Summary: 1 breaking, 0 warning(s), 1 non-breaking change(s).
Compatibility result: BREAKING CHANGES DETECTED
```

### JSON Schema

Compare JSON Schema documents explicitly with `--format json-schema`:

```shell
ict compare request-v1.schema.json request-v2.schema.json --format json-schema
```

This mode compares a schema as a request contract. OpenAPI mode compares operation parameters and JSON request/response schemas.

## What is compared

- OpenAPI paths and HTTP methods
- Parameter presence, requiredness, types, and enum values
- Request body presence, requiredness, and JSON schema
- Response status codes and JSON response schemas
- Object properties, required properties, array items, types, and enum values
- JSON Schema files using the same supported schema subset

New optional fields, optional parameters, operations, and response codes are reported as non-breaking. Removed operations and response fields, newly required request fields, and restrictive request/response schema changes are reported as breaking.

## Limitations

This is a conservative structural checker, not a proof of runtime compatibility. It reports warnings for schema constructs that it does not evaluate, including unresolved external references, JSON Schema composition, and constraints such as `format`, ranges, lengths, and patterns. External OpenAPI references are not fetched. Response headers and parameter serialization changes are also outside the compatibility analysis. OpenAPI features that do not use JSON request/response media types are not schema-compared. Some changes depend on server behavior or client tolerance and therefore cannot be classified with certainty.

Review every warning and validate important changes against representative consumers and providers. The tool does not infer semantic changes from descriptions, operation IDs, authentication behavior, or example payloads.

## Privacy

The CLI reads contract files from the local filesystem and performs comparison locally. It makes no HTTP requests and does not transmit contract contents, customer payloads, or credentials; external OpenAPI references are deliberately not resolved. Do not pass live customer payloads as schema files; use sanitized contract definitions instead.

## Development

```shell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

## License

MIT. See [LICENSE](LICENSE).
