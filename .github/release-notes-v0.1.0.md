# Integration Contract Tester v0.1.0

The first public release of Integration Contract Tester provides a local .NET CLI for reviewing API contract changes before deployment.

## Included

- Compare OpenAPI 3.0 contracts in JSON or YAML.
- Compare JSON Schema documents using `--format json-schema`.
- Report confirmed breaking changes, non-breaking additions, and unsupported cases as warnings.
- Classify request and response changes by direction, including enum, type, requiredness, and property changes.
- Treat path templates that differ only in parameter names (`/orders/{id}` and `/orders/{orderId}`) as the same operation.
- Return CI-friendly exit codes: `0` no breaking changes, `1` breaking changes found, `2` invalid input or usage.
- Print the installed version with `ict --version`.
- Keep contract comparison local; the CLI does not make network requests or transmit input files.

## Known limitations

OpenAPI 3.1 is not yet supported. Schema references/composition, format assertions, and several advanced JSON Schema constraints are not evaluated. Review warnings and validate changes with real consumers before relying on a compatibility result.

## Requirements and installation

Requires .NET 10.0 or later. Download `JspIt.IntegrationContractTester.0.1.0.nupkg` from this release, then install it from the download folder:

```shell
gh release download v0.1.0 --repo JSP-IT/integration-contract-tester --pattern "*.nupkg" --dir ./downloads
dotnet tool install --global JspIt.IntegrationContractTester --version 0.1.0 --add-source ./downloads
```
