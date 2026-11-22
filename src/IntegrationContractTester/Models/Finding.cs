namespace IntegrationContractTester.Models;

internal enum FindingSeverity
{
    Breaking,
    Warning,
    NonBreaking
}

internal sealed record Finding(
    FindingSeverity Severity,
    string Code,
    string Location,
    string Message);
