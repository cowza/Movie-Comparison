namespace MovieComparison.FakeUpstream;

public static class Entry
{
    public static Task Main(string[] args)
    {
        var token = Environment.GetEnvironmentVariable("FAKE_UPSTREAM_TOKEN") ?? "fake-token";
        return FakeUpstreamApp.Build(new FakeUpstreamState(), token, args).RunAsync();
    }
}
