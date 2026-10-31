namespace MovieComparison.IntegrationTests;

public class OpenApiContractTests
{
    /// <summary>
    /// The frontend's types are generated from the committed document, so it must match the running API.
    /// After an intended API change, run `make openapi`.
    /// </summary>
    [Fact]
    public async Task CommittedDocument_MatchesTheRunningApi()
    {
        await using var stack = await TestStack.StartAsync();
        var actual = await stack.Client.GetStringAsync("/openapi/v1.json");
        var path = Path.Combine(FindBackendRoot(), "openapi", "v1.json");

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, actual);
        }

        Assert.Equal(await File.ReadAllTextAsync(path), actual);
    }

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory!.FullName, "MovieComparison.sln")))
        {
            directory = directory.Parent;
        }
        return directory.FullName;
    }
}
