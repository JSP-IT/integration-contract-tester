using IntegrationContractTester.Models;

namespace IntegrationContractTester;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        if (args[0] != "compare" || args.Length < 3)
        {
            PrintUsage();
            return 2;
        }

        var format = "openapi";
        for (var index = 3; index < args.Length; index++)
        {
            if (args[index] == "--format" && index + 1 < args.Length)
            {
                format = args[++index].ToLowerInvariant();
            }
            else
            {
                Console.Error.WriteLine($"Invalid option or missing value: {args[index]}");
                PrintUsage();
                return 2;
            }
        }

        if (format is not ("openapi" or "json-schema"))
        {
            Console.Error.WriteLine($"Unsupported format '{format}'. Use 'openapi' or 'json-schema'.");
            return 2;
        }

        try
        {
            var baseline = ContractReader.Read(args[1], format);
            var candidate = ContractReader.Read(args[2], format);
            var findings = ContractComparer.Compare(baseline, candidate);
            PrintReport(args[1], args[2], findings);
            return findings.Any(finding => finding.Severity == FindingSeverity.Breaking) ? 1 : 0;
        }
        catch (FileNotFoundException exception)
        {
            Console.Error.WriteLine($"Input file not found: {exception.FileName ?? exception.Message}");
            return 2;
        }
        catch (DirectoryNotFoundException exception)
        {
            Console.Error.WriteLine($"Input directory not found: {exception.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine($"Cannot read input: {exception.Message}");
            return 2;
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine($"Invalid contract: {exception.Message}");
            return 2;
        }
        catch (System.Text.Json.JsonException exception)
        {
            Console.Error.WriteLine($"Malformed JSON: {exception.Message}");
            return 2;
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"Input error: {exception.Message}");
            return 2;
        }
    }

    private static void PrintReport(string baseline, string candidate, IReadOnlyList<Finding> findings)
    {
        Console.WriteLine($"Contract comparison: {Path.GetFileName(baseline)} -> {Path.GetFileName(candidate)}");
        Console.WriteLine();

        if (findings.Count == 0)
        {
            Console.WriteLine("No compatibility changes detected in the supported contract surface.");
        }
        else
        {
            foreach (var finding in findings)
            {
                var label = finding.Severity switch
                {
                    FindingSeverity.Breaking => "BREAKING",
                    FindingSeverity.Warning => "WARNING",
                    _ => "NON-BREAKING"
                };
                Console.WriteLine($"[{label}] {finding.Code} at {finding.Location}: {finding.Message}");
            }
        }

        var breaking = findings.Count(finding => finding.Severity == FindingSeverity.Breaking);
        var warnings = findings.Count(finding => finding.Severity == FindingSeverity.Warning);
        var nonBreaking = findings.Count(finding => finding.Severity == FindingSeverity.NonBreaking);
        Console.WriteLine();
        Console.WriteLine($"Summary: {breaking} breaking, {warnings} warning(s), {nonBreaking} non-breaking change(s).");
        Console.WriteLine(breaking > 0
            ? "Compatibility result: BREAKING CHANGES DETECTED"
            : warnings > 0
                ? "Compatibility result: NO CONFIRMED BREAKING CHANGES; REVIEW WARNINGS"
                : "Compatibility result: NO BREAKING CHANGES DETECTED");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Integration Contract Tester");
        Console.WriteLine("Usage: ict compare <baseline> <candidate> [--format openapi|json-schema]");
        Console.WriteLine("The format defaults to openapi. JSON Schema input requires --format json-schema.");
    }
}
