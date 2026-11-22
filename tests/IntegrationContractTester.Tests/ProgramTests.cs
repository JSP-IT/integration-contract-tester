using Xunit;

namespace IntegrationContractTester.Tests;

/// <summary>
/// Exercises the documented CLI contract. Console redirection is process-wide, so these tests run in a
/// non-parallel collection.
/// </summary>
[Collection(nameof(ConsoleCollection))]
public sealed class ProgramTests
{
    private static readonly string BaselineExample = Path.Combine(AppContext.BaseDirectory, "examples", "openapi-v1.json");
    private static readonly string CandidateExample = Path.Combine(AppContext.BaseDirectory, "examples", "openapi-v2.json");

    [Fact]
    public void BreakingChangesReturnExitCodeOne()
    {
        var result = Run("compare", BaselineExample, CandidateExample);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Summary: 1 breaking, 0 warning(s), 2 non-breaking change(s).", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void IdenticalContractsReturnExitCodeZero()
    {
        var result = Run("compare", BaselineExample, BaselineExample);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NO BREAKING CHANGES DETECTED", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("compare", "--format", "openapi", "{baseline}", "{candidate}")]
    [InlineData("compare", "{baseline}", "--format=openapi", "{candidate}")]
    [InlineData("compare", "{baseline}", "{candidate}", "--format", "OpenAPI")]
    [InlineData("compare", "--", "{baseline}", "{candidate}")]
    public void OptionsAreAcceptedInAnyPosition(params string[] arguments)
    {
        var result = Run(arguments
            .Select(argument => argument
                .Replace("{baseline}", BaselineExample, StringComparison.Ordinal)
                .Replace("{candidate}", CandidateExample, StringComparison.Ordinal))
            .ToArray());

        Assert.Equal(1, result.ExitCode);
    }

    [Theory]
    [InlineData]
    [InlineData("diff", "a", "b")]
    [InlineData("compare", "only-one-file")]
    [InlineData("compare", "a", "b", "c")]
    [InlineData("compare", "a", "b", "--format")]
    [InlineData("compare", "a", "b", "--unknown")]
    [InlineData("compare", "a", "b", "--format", "wsdl")]
    public void UsageErrorsReturnExitCodeTwo(params string[] arguments)
    {
        Assert.Equal(2, Run(arguments).ExitCode);
    }

    [Fact]
    public void MissingFileReturnsExitCodeTwo()
    {
        var result = Run("compare", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), CandidateExample);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Input file not found", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidOpenApiReturnsExitCodeTwo()
    {
        using var files = new TempFiles();
        var invalid = files.Write("garbage: [");

        Assert.Equal(2, Run("compare", invalid, CandidateExample).ExitCode);
    }

    [Fact]
    public void UnsupportedOpenApiVersionReturnsExitCodeTwoWithInputError()
    {
        using var files = new TempFiles();
        var openApi31 = files.Write("""
            {"openapi":"3.1.0","info":{"title":"t","version":"1"},"paths":{}}
            """);

        var result = Run("compare", openApi31, openApi31);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid contract", result.Error, StringComparison.Ordinal);
        Assert.Contains("3.1.0", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionPrintsPackageVersion()
    {
        var result = Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", result.Output.Trim());
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("compare", "--help")]
    public void HelpReturnsExitCodeZero(params string[] arguments)
    {
        var result = Run(arguments);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: ict compare", result.Output, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] arguments)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exitCode = Program.Main(arguments);
            return (exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}

[CollectionDefinition(nameof(ConsoleCollection), DisableParallelization = true)]
public sealed class ConsoleCollection;
