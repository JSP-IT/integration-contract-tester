# Integration Contract Tester v0.1.0

The first public release of Integration Contract Tester provides a local .NET CLI for reviewing API contract changes before deployment.

## Included

- Compare OpenAPI 3 contracts in JSON or YAML.
- Compare JSON Schema documents using `--format json-schema`.
- Report confirmed breaking changes, non-breaking additions, and unsupported cases as warnings.
- Return a CI-friendly exit code when breaking changes are detected.
- Keep contract comparison local; the CLI does not make network requests or transmit input files.

## Known limitations

Schema references/composition, format assertions, and several advanced JSON Schema constraints are not evaluated. Review warnings and validate changes with real consumers before relying on a compatibility result.

## Requirements and installation

Requires .NET 10.0 or later. Once the tool package is published:

```shell
dotnet tool install --global JspIt.IntegrationContractTester --version 0.1.0
```
