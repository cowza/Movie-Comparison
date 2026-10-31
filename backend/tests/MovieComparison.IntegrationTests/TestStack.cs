using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MovieComparison.Core.DTOs;
using MovieComparison.Core.Interfaces;
using MovieComparison.FakeUpstream;
using MovieComparison.Infrastructure.Snapshots;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MovieComparison.IntegrationTests;

/// <summary>The real app and its real HTTP pipeline, talking to a fake upstream over a real socket.</summary>
public sealed class TestStack : IAsyncDisposable
{
    public const string Token = "test-token";

    private readonly WebApplication _upstream;
    private readonly WebApplicationFactory<Program> _factory;

    private TestStack(WebApplication upstream, WebApplicationFactory<Program> factory, FakeUpstreamState state)
    {
        _upstream = upstream;
        _factory = factory;
        Upstream = state;
        Client = factory.CreateClient();
    }

    public FakeUpstreamState Upstream { get; }
    public HttpClient Client { get; }

    public static async Task<TestStack> StartAsync(
        Action<FakeUpstreamState>? arrange = null,
        Dictionary<string, string>? settings = null,
        bool waitForFirstCycle = true)
    {
        var state = new FakeUpstreamState();
        arrange?.Invoke(state);

        var upstream = FakeUpstreamApp.Build(state, Token);
        upstream.Urls.Add("http://127.0.0.1:0");
        await upstream.StartAsync();

        var values = new Dictionary<string, string>
        {
            ["ExternalApiSettings:BaseUrl"] = upstream.Urls.First(),
            ["ExternalApiSettings:ApiToken"] = Token,
            // Long enough that the worker runs once and each test drives further cycles itself.
            ["Snapshot:RefreshInterval"] = "01:00:00",
            ["Snapshot:FailureRetryInterval"] = "01:00:00",
            ["ProviderResilience:AttemptTimeout"] = "00:00:00.300",
            ["ProviderResilience:TotalTimeout"] = "00:00:05",
            ["ProviderResilience:RetryDelay"] = "00:00:00.001",
            // Most tests are about what the retries do, so the breaker stays out of the way unless asked for.
            ["ProviderResilience:CircuitMinimumThroughput"] = "100000",
            ["RateLimiting:PermitsPerMinute"] = "100000",
        };
        foreach (var (key, value) in settings ?? [])
        {
            values[key] = value;
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            foreach (var (key, value) in values)
            {
                builder.UseSetting(key, value);
            }
        });

        var stack = new TestStack(upstream, factory, state);
        if (waitForFirstCycle)
        {
            await stack.WaitForAsync(state.Mode("cinemaworld") == FakeUpstreamState.Hang ? [] : ["cinemaworld"]);
            await stack.WaitForAsync(state.Mode("filmworld") == FakeUpstreamState.Hang ? [] : ["filmworld"]);
        }
        return stack;
    }

    public async Task WaitForAsync(IEnumerable<string> providers)
    {
        var store = _factory.Services.GetRequiredService<ISnapshotStore>();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (providers.Any(provider => store.Get(provider) is null))
        {
            Assert.True(DateTime.UtcNow < deadline, "First refresh cycle did not finish in time");
            await Task.Delay(10);
        }
    }

    public async Task RefreshAsync()
    {
        var refresher = _factory.Services.GetRequiredService<ProviderRefresher>();
        var providers = _factory.Services.GetServices<IExternalMovieProvider>();
        await Task.WhenAll(providers.Select(provider => refresher.RefreshAsync(provider, CancellationToken.None)));
    }

    public async Task<MovieOffersDto> GetOffersAsync(string id)
    {
        var offers = await Client.GetFromJsonAsync<MovieOffersDto>($"/api/movies/{id}/offers");
        return offers!;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await _upstream.StopAsync(shutdown.Token);
        await _upstream.DisposeAsync();
    }
}
