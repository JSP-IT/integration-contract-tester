using System.Reflection;
using IntegrationContractTester.Models;

namespace IntegrationContractTester;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage(Console.Error);
            return 2;
        }

        if (args[0] is "--help" or "-h" or "help")
        {
            PrintUsage(Console.Out);
            return 0;
        }

        if (args[0] is "--version")
        {
            Console.WriteLine(GetVersion());
            return 0;
        }

        if (args[0] != "compare")
        {
            Console.Error.WriteLine($"Unknown command: {args[0]}");
            PrintUsage(Console.Error);
            return 2;
        }

        var format = "openapi";
        var files = new List<string>();
        var onlyFilesFollow = false;
        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (onlyFilesFollow || !argument.StartsWith('-') || argument == "-")
            {
                files.Add(argument);
            }
            else if (argument == "--")
            {
                onlyFilesFollow = true;
            }
            else if (argument is "--help" or "-h")
            {
                PrintUsage(Console.Out);
                return 0;
            }
            else if (argument == "--format" && index + 1 < args.Length)
            {
                format = args[++index].ToLowerInvariant();
            }
            else if (argument.StartsWith("--format=", StringComparison.Ordinal))
            {
                format = argument["--format=".Length..].ToLowerInvariant();
            }
            else
            {
                Console.Error.WriteLine($"Invalid option or missing value: {argument}");
                PrintUsage(Console.Error);
                return 2;
            }
        }

        if (files.Count != 2)
        {
            Console.Error.WriteLine($"Expected a baseline and a candidate file, but got {files.Count} file argument(s).");
            PrintUsage(Console.Error);
            return 2;
        }

        if (format is not ("openapi" or "json-schema"))
        {
            Console.Error.WriteLine($"Unsupported format '{format}'. Use 'openapi' or 'json-schema'.");
            return 2;
        }

        try
        {
            var baseline = ContractReader.Read(files[0], format);
            var candidate = ContractReader.Read(files[1], format);
            var findings = ContractComparer.Compare(baseline, candidate);
            PrintReport(files[0], files[1], findings);
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
        catch (Exception exception)
        {
            // Keep the documented exit-code contract: never let an unexpected failure look like exit code 1 or 0.
            Console.Error.WriteLine($"Unexpected error ({exception.GetType().Name}): {exception.Message}");
            return 2;
        }
    }

    private static string GetVersion()
    {
        var version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var metadataStart = version.IndexOf('+', StringComparison.Ordinal);
        return metadataStart >= 0 ? version[..metadataStart] : version;
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

    private static void PrintUsage(TextWriter output)
    {
        output.WriteLine($"Integration Contract Tester {GetVersion()}");
        output.WriteLine("Usage: ict compare <baseline> <candidate> [--format openapi|json-schema]");
        output.WriteLine("       ict --version");
        output.WriteLine("The format defaults to openapi. JSON Schema input requires --format json-schema.");
        output.WriteLine("Exit codes: 0 = no breaking changes, 1 = breaking changes found, 2 = invalid input or usage.");
    }
}
