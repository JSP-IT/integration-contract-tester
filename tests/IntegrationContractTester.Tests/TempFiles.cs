namespace IntegrationContractTester.Tests;

/// <summary>Creates temporary contract files and deletes them on dispose.</summary>
internal sealed class TempFiles : IDisposable
{
    private readonly List<string> paths = [];

    public string Write(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ict-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path, content);
        paths.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in paths)
        {
            File.Delete(path);
        }
    }
}
