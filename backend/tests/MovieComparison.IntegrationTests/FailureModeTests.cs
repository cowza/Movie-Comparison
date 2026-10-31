using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using MovieComparison.Core.DTOs;
using MovieComparison.FakeUpstream;

namespace MovieComparison.IntegrationTests;

/// <summary>The acceptance spec: docs/DECISIONS.md lists these failure modes and why each behaves this way.</summary>
public class FailureModeTests
{
    private const string BothProviders = "0076759";
    private const string CinemaWorldOnly = "2488496";

    [Fact]
    public async Task Healthy_ReturnsEveryOfferWithTheCheapestAsBest()
    {
        await using var stack = await TestStack.StartAsync();

        var offers = await stack.GetOffersAsync(BothProviders);

        Assert.Equal("filmworld", offers.BestOffer.Provider);
        Assert.Equal(20.0m, offers.BestOffer.Price);
        Assert.Equal(["filmworld", "cinemaworld"], offers.Offers.Select(o => o.Provider));
        Assert.True(offers.Complete);
        Assert.Empty(offers.UnavailableProviders);
    }

    [Fact]
    public async Task Healthy_MergesMoviesOnCanonicalId()
    {
        await using var stack = await TestStack.StartAsync();

        var list = await stack.Client.GetFromJsonAsync<MovieListDto>("/api/movies");

        Assert.Equal(7, list!.Movies.Count);
        Assert.True(list.Complete);
        Assert.Equal(2, list.Movies.Single(m => m.Id == BothProviders).Providers.Count);
    }

    [Fact]
    public async Task FailingProvider_GetsFourAttempts_NotSixteen()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.Error));

        Assert.Equal(4, stack.Upstream.Hits("filmworld"));
    }

    [Fact]
    public async Task SecondProvider_DoesNotInheritTheFirstProvidersOutage()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.Error));

        // One catalogue call plus one price call per movie, with no retries.
        Assert.Equal(8, stack.Upstream.Hits("cinemaworld"));
    }

    [Fact]
    public async Task MovieOnlyOneProviderSells_IsCompleteAndNeverRequestedFromTheOther()
    {
        await using var stack = await TestStack.StartAsync();

        var offers = await stack.GetOffersAsync(CinemaWorldOnly);

        Assert.Equal("cinemaworld", offers.BestOffer.Provider);
        Assert.Single(offers.Offers);
        Assert.True(offers.Complete);
        Assert.DoesNotContain(stack.Upstream.Requests, path => path.Contains($"fw{CinemaWorldOnly}"));
    }

    [Fact]
    public async Task ProviderDownFromColdStart_IsReportedRatherThanHidden()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.Error));

        var offers = await stack.GetOffersAsync(BothProviders);

        Assert.Equal("cinemaworld", offers.BestOffer.Provider);
        Assert.False(offers.Complete);
        Assert.Equal(["filmworld"], offers.UnavailableProviders);
    }

    [Fact]
    public async Task CheaperProviderGoesDown_KeepsItsLastPriceFlaggedStale()
    {
        await using var stack = await TestStack.StartAsync(settings: new() { ["Snapshot:StaleAfter"] = "00:00:00.500" });

        stack.Upstream.SetMode("filmworld", FakeUpstreamState.Error);
        await Task.Delay(600);
        await stack.RefreshAsync();

        var offers = await stack.GetOffersAsync(BothProviders);

        Assert.Equal("filmworld", offers.BestOffer.Provider);
        Assert.Equal(20.0m, offers.BestOffer.Price);
        Assert.True(offers.BestOffer.Stale);
        Assert.False(offers.Offers.Single(o => o.Provider == "cinemaworld").Stale);
        Assert.False(offers.Complete);
        Assert.Equal(["filmworld"], offers.UnavailableProviders);
    }

    [Fact]
    public async Task HangingProvider_DoesNotDelayRequests()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.Hang));

        var timer = Stopwatch.StartNew();
        var offers = await stack.GetOffersAsync(BothProviders);
        timer.Stop();

        Assert.Equal("cinemaworld", offers.BestOffer.Provider);
        Assert.True(timer.ElapsedMilliseconds < 250, $"Took {timer.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task HangingProvider_TimeoutsAreRetried()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.Hang));

        await stack.WaitForAsync(["filmworld"]);

        Assert.Equal(4, stack.Upstream.Hits("filmworld"));
    }

    [Fact]
    public async Task StalledResponseBody_TimesOutAndRetriesInsteadOfWedgingTheRefresh()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.StallBody));

        await stack.WaitForAsync(["filmworld"]);

        Assert.Equal(4, stack.Upstream.Hits("filmworld"));
    }

    [Fact]
    public async Task FlakyProvider_SucceedsThroughRetries()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.Flaky));

        var offers = await stack.GetOffersAsync(BothProviders);

        Assert.Equal("filmworld", offers.BestOffer.Provider);
        Assert.True(offers.Complete);
    }

    [Fact]
    public async Task EveryPriceFailingFromColdStart_CountsAsNoData()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetAllModes(FakeUpstreamState.PricesError));

        var list = await stack.Client.GetAsync("/api/movies");
        var ready = await stack.Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, list.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    [Fact]
    public async Task EveryPriceFailingForOneProvider_IsReportedAsUnavailable()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.PricesError));

        var list = await stack.Client.GetFromJsonAsync<MovieListDto>("/api/movies");

        Assert.False(list!.Complete);
        Assert.Equal(["filmworld"], list.UnavailableProviders);
    }

    [Fact]
    public async Task BothDownFromColdStart_Returns503WithRetryAfter()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetAllModes(FakeUpstreamState.Error));

        var list = await stack.Client.GetAsync("/api/movies");
        var offers = await stack.Client.GetAsync($"/api/movies/{BothProviders}/offers");
        var ready = await stack.Client.GetAsync("/health/ready");
        var live = await stack.Client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, list.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, offers.StatusCode);
        Assert.NotNull(list.Headers.RetryAfter);
        Assert.Equal("application/problem+json", list.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task BothDownAfterLoading_KeepsServingLastGoodData()
    {
        await using var stack = await TestStack.StartAsync(settings: new() { ["Snapshot:StaleAfter"] = "00:00:00.200" });

        stack.Upstream.SetAllModes(FakeUpstreamState.Error);
        await stack.RefreshAsync();
        await Task.Delay(300);

        var offers = await stack.GetOffersAsync(BothProviders);
        var list = await stack.Client.GetFromJsonAsync<MovieListDto>("/api/movies");
        var ready = await stack.Client.GetAsync("/health/ready");

        Assert.Equal(2, offers.Offers.Count);
        Assert.All(offers.Offers, offer => Assert.True(offer.Stale));
        Assert.False(list!.Complete);
        Assert.Equal(7, list.Movies.Count);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Degraded", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProviderRecovers_NextCycleRestoresTheFullResult()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.Error));

        var during = await stack.Client.GetFromJsonAsync<MovieListDto>("/api/movies");
        stack.Upstream.SetMode("filmworld", FakeUpstreamState.Ok);
        await stack.RefreshAsync();
        var after = await stack.Client.GetFromJsonAsync<MovieListDto>("/api/movies");

        Assert.False(during!.Complete);
        Assert.Single(during.Movies.Single(m => m.Id == BothProviders).Providers);
        Assert.True(after!.Complete);
        Assert.Equal(2, after.Movies.Single(m => m.Id == BothProviders).Providers.Count);
    }

    [Fact]
    public async Task ConcurrentRequests_CauseNoUpstreamCalls()
    {
        await using var stack = await TestStack.StartAsync();
        stack.Upstream.ClearRequests();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            stack.Client.GetAsync($"/api/movies/{BothProviders}/offers")));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Empty(stack.Upstream.Requests);
    }

    [Theory]
    [InlineData("9999999")]
    [InlineData("..%2F..%2Fmovies")]
    [InlineData("%2F..%2F..%2Fmovies")]
    public async Task UnknownIds_Return404WithoutReachingUpstream(string id)
    {
        await using var stack = await TestStack.StartAsync();
        stack.Upstream.ClearRequests();

        var response = await stack.Client.GetAsync($"/api/movies/{id}/offers");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(stack.Upstream.Requests);
    }

    [Fact]
    public async Task ClientErrorsFromUpstream_AreNotRetried()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.PricesMissing));

        // One catalogue call plus one price call per movie sold.
        Assert.Equal(7, stack.Upstream.Hits("filmworld"));
    }

    [Fact]
    public async Task InvalidPrice_KeepsTheLastGoodPrice()
    {
        await using var stack = await TestStack.StartAsync();

        stack.Upstream.SetMode("filmworld", FakeUpstreamState.PricesInvalid);
        await stack.RefreshAsync();

        var offers = await stack.GetOffersAsync(BothProviders);
        Assert.Equal(20.0m, offers.Offers.Single(o => o.Provider == "filmworld").Price);
    }

    [Fact]
    public async Task UnparseablePriceFromColdStart_IsLeftOutRatherThanShownAsNull()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.PricesInvalid));

        var offers = await stack.GetOffersAsync(BothProviders);

        Assert.Equal(["cinemaworld"], offers.Offers.Select(o => o.Provider));
        Assert.False(offers.Complete);
    }

    [Theory]
    [InlineData(FakeUpstreamState.Empty)]
    [InlineData(FakeUpstreamState.Garbage)]
    public async Task BadCatalogue_DoesNotWipeTheSnapshot(string mode)
    {
        await using var stack = await TestStack.StartAsync();

        stack.Upstream.SetMode("filmworld", mode);
        await stack.RefreshAsync();

        var offers = await stack.GetOffersAsync(BothProviders);
        Assert.Equal(2, offers.Offers.Count);
    }

    [Fact]
    public async Task RejectedToken_IsNotRetriedAndNeverLeaks()
    {
        await using var stack = await TestStack.StartAsync(settings: new() { ["ExternalApiSettings:ApiToken"] = "wrong-token" });

        var response = await stack.Client.GetAsync("/api/movies");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("wrong-token", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, stack.Upstream.Hits("filmworld"));
    }

    [Fact]
    public async Task CircuitBreaker_StopsCallingAProviderThatKeepsFailing()
    {
        await using var stack = await TestStack.StartAsync(
            state => state.SetMode("filmworld", FakeUpstreamState.Error),
            new() { ["ProviderResilience:CircuitMinimumThroughput"] = "4" });

        Assert.Equal(4, stack.Upstream.Hits("filmworld"));

        await stack.RefreshAsync();

        Assert.Equal(4, stack.Upstream.Hits("filmworld"));
    }

    [Fact]
    public async Task CircuitBreaker_AlsoTripsOnHangs()
    {
        await using var stack = await TestStack.StartAsync(
            state => state.SetMode("filmworld", FakeUpstreamState.Hang),
            new() { ["ProviderResilience:CircuitMinimumThroughput"] = "4" });

        await stack.WaitForAsync(["filmworld"]);
        Assert.Equal(4, stack.Upstream.Hits("filmworld"));

        await stack.RefreshAsync();

        Assert.Equal(4, stack.Upstream.Hits("filmworld"));
    }

    [Fact]
    public async Task UpstreamRedirect_IsNotFollowedSoTheTokenStaysWithTheProvider()
    {
        await using var stack = await TestStack.StartAsync(state => state.SetMode("filmworld", FakeUpstreamState.Redirect));

        Assert.DoesNotContain(stack.Upstream.Requests, path => path.EndsWith("redirect-target"));
        Assert.Equal(1, stack.Upstream.Hits("filmworld"));
    }

    [Fact]
    public async Task RateLimit_RejectsExcessRequests()
    {
        await using var stack = await TestStack.StartAsync(settings: new() { ["RateLimiting:PermitsPerMinute"] = "2" });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await stack.Client.GetAsync("/api/movies")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404()
    {
        await using var stack = await TestStack.StartAsync();

        var response = await stack.Client.GetAsync("/api/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MissingToken_FailsAtStartup()
    {
        var failure = await Record.ExceptionAsync(async () =>
        {
            await using var stack = await TestStack.StartAsync(settings: new() { ["ExternalApiSettings:ApiToken"] = "" });
        });

        Assert.Contains("ApiToken", failure?.ToString());
    }
}
