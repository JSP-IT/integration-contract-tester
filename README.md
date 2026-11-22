# Integration Contract Tester

Integration Contract Tester is a local .NET command-line tool that compares API contracts before deployment. It highlights likely breaking changes separately from compatible additions and schema features it cannot safely evaluate, helping teams catch integration regressions during code review or CI.

## Requirements

- .NET SDK 10.0 or later
- Windows, Linux, or macOS

## Install

The tool is distributed as a .NET tool package attached to each [GitHub Release](https://github.com/JSP-IT/integration-contract-tester/releases). Download the `.nupkg` file from the release, then install it from the download folder:

```shell
dotnet tool install --global JspIt.IntegrationContractTester --version 0.1.0 --add-source ./downloads
ict --version
```

With the GitHub CLI, the download and install steps are:

```shell
gh release download v0.1.0 --repo JSP-IT/integration-contract-tester --pattern "*.nupkg" --dir ./downloads
dotnet tool install --global JspIt.IntegrationContractTester --version 0.1.0 --add-source ./downloads
```

To pin the tool for a repository or CI job, install it into a local tool manifest instead:

```shell
dotnet new tool-manifest
dotnet tool install JspIt.IntegrationContractTester --version 0.1.0 --add-source ./downloads
dotnet tool run ict compare baseline.yaml candidate.yaml
```

Or build and run from a source checkout:

```shell
dotnet run --project src/IntegrationContractTester -- compare examples/openapi-v1.json examples/openapi-v2.json
```

## Compare API versions

```shell
ict compare baseline.yaml candidate.yaml
```

OpenAPI 3.0 is the default format, in JSON or YAML. OpenAPI 3.1 documents are not yet supported and are rejected as invalid input. Options may appear before or after the file arguments, and `--format json-schema` can also be written as `--format=json-schema`.

The report lists each finding as `BREAKING`, `NON-BREAKING`, or `WARNING`. Exit codes:

| Code | Meaning |
| --- | --- |
| `0` | No confirmed breaking changes (warnings may still need review). |
| `1` | At least one breaking change was found. |
| `2` | Invalid input, unreadable file, or incorrect command usage. |

Example:

```shell
ict compare examples/openapi-v1.json examples/openapi-v2.json
```

Report:

```text
Contract comparison: openapi-v1.json -> openapi-v2.json

[BREAKING] RESPONSE_PROPERTY_REMOVED at GET /orders/{id} response 200.status: A response property was removed.
[NON-BREAKING] RESPONSE_PROPERTY_ADDED at GET /orders/{id} response 200.total: A response property was added.
[NON-BREAKING] OPTIONAL_PROPERTY_ADDED at POST /orders request body.note: An optional request property was added.

Summary: 1 breaking, 0 warning(s), 2 non-breaking change(s).
Compatibility result: BREAKING CHANGES DETECTED
```

### Use in CI

The exit code lets a pipeline fail on breaking changes. For example, in GitHub Actions, compare the contract on the pull request branch against the version on `main`:

```yaml
- run: git fetch --depth=1 origin main
- run: git show origin/main:openapi.yaml > baseline.yaml
- run: ict compare baseline.yaml openapi.yaml
```

### JSON Schema

Compare JSON Schema documents explicitly with `--format json-schema`:

```shell
ict compare request-v1.schema.json request-v2.schema.json --format json-schema
```

This mode compares a schema as a request contract. OpenAPI mode compares operation parameters and JSON request/response schemas.

## What is compared

- OpenAPI paths and HTTP methods (renaming a path parameter, such as `{id}` to `{orderId}`, is not reported as a change)
- Parameter presence, requiredness, types, and enum values
- Request body presence, requiredness, and JSON schema
- Response status codes and JSON response schemas
- Object properties, required properties, array items, types, and enum values
- JSON Schema files using the same supported schema subset

Changes are classified by direction. A request schema breaks clients when it accepts less than before; a response schema breaks clients when it may return something they did not expect. For example, removing an enum value is breaking in a request, while adding an enum value is breaking in a response.

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
dotnet pack src/IntegrationContractTester --configuration Release --output artifacts
```

## License

MIT. See [LICENSE](LICENSE).
