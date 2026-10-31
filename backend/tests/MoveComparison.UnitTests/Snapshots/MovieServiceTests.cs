using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MovieComparison.Core.DTOs;
using MovieComparison.Core.Models;
using MovieComparison.Infrastructure.Configuration;
using MovieComparison.Infrastructure.Services;
using MovieComparison.Infrastructure.Snapshots;

namespace MoveComparison.UnitTests.Snapshots;

public class MovieServiceTests
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxDataAge = TimeSpan.FromHours(24);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly InMemorySnapshotStore _store = new();
    private readonly MovieService _service;

    public MovieServiceTests()
    {
        _service = new MovieService(
            [new StubProvider("cinemaworld"), new StubProvider("filmworld")],
            _store,
            Options.Create(new SnapshotSettings { StaleAfter = StaleAfter, MaxDataAge = MaxDataAge }),
            _time);
    }

    private void Save(string provider, params (string Id, decimal? Price)[] movies)
    {
        var now = _time.GetUtcNow();
        _store.Save(new ProviderSnapshot(
            provider,
            movies.ToDictionary(
                m => m.Id,
                m => new SnapshotMovie(
                    new ProviderMovie(m.Id, $"Movie {m.Id}", "1977", null),
                    m.Price is { } price ? new ProviderOffer(price, now) : null)),
            now,
            null));
    }

    [Fact]
    public void GetOffers_PicksTheLowestPrice()
    {
        Save("cinemaworld", ("1", 30m));
        Save("filmworld", ("1", 20m));

        var result = _service.GetOffers("1");

        Assert.Equal(OffersStatus.Found, result.Status);
        Assert.Equal("filmworld", result.Offers!.BestOffer.Provider);
    }

    [Fact]
    public void GetOffers_WhenNothingLoaded_IsUnavailable()
    {
        Assert.Equal(OffersStatus.Unavailable, _service.GetOffers("1").Status);
        Assert.Null(_service.GetMovies());
    }

    [Fact]
    public void GetOffers_WhenEveryProviderIsFreshAndNoneSellIt_IsNotFound()
    {
        Save("cinemaworld", ("1", 30m));
        Save("filmworld", ("1", 20m));

        Assert.Equal(OffersStatus.NotFound, _service.GetOffers("2").Status);
    }

    [Fact]
    public void GetOffers_WhenAProviderIsDownAndNoOtherSellsIt_IsUnavailableNotNotFound()
    {
        Save("cinemaworld", ("1", 30m));

        Assert.Equal(OffersStatus.Unavailable, _service.GetOffers("2").Status);
    }

    [Fact]
    public void GetOffers_AtTheStaleBoundary_IsStillFresh()
    {
        Save("cinemaworld", ("1", 30m));
        Save("filmworld", ("1", 20m));
        _time.Advance(StaleAfter);

        var offers = _service.GetOffers("1").Offers!;

        Assert.True(offers.Complete);
        Assert.All(offers.Offers, offer => Assert.False(offer.Stale));
    }

    [Fact]
    public void GetOffers_PastStaleAfter_FlagsOffersAndIncompleteness()
    {
        Save("cinemaworld", ("1", 30m));
        Save("filmworld", ("1", 20m));
        _time.Advance(StaleAfter + TimeSpan.FromSeconds(1));

        var offers = _service.GetOffers("1").Offers!;

        Assert.False(offers.Complete);
        Assert.All(offers.Offers, offer => Assert.True(offer.Stale));
        Assert.Equal(["cinemaworld", "filmworld"], offers.UnavailableProviders);
    }

    [Fact]
    public void GetOffers_PastMaxDataAge_DropsTheData()
    {
        Save("cinemaworld", ("1", 30m));
        _time.Advance(MaxDataAge + TimeSpan.FromSeconds(1));
        Save("filmworld", ("1", 20m));

        var offers = _service.GetOffers("1").Offers!;

        Assert.Equal(["filmworld"], offers.Offers.Select(o => o.Provider));
        Assert.Equal(["cinemaworld"], offers.UnavailableProviders);
    }

    [Fact]
    public void GetOffers_WhenAllDataIsPastMaxAge_IsUnavailable()
    {
        Save("cinemaworld", ("1", 30m));
        Save("filmworld", ("1", 20m));
        _time.Advance(MaxDataAge + TimeSpan.FromSeconds(1));

        Assert.Equal(OffersStatus.Unavailable, _service.GetOffers("1").Status);
        Assert.Null(_service.GetMovies());
    }

    [Fact]
    public void GetOffers_MovieListedWithoutAPrice_MarksThatProviderUnavailable()
    {
        Save("cinemaworld", ("1", 30m));
        Save("filmworld", ("1", null));

        var offers = _service.GetOffers("1").Offers!;

        Assert.Single(offers.Offers);
        Assert.False(offers.Complete);
        Assert.Equal(["filmworld"], offers.UnavailableProviders);
    }

    [Fact]
    public void GetOffers_MovieListedButNeverPricedAnywhere_IsUnavailable()
    {
        Save("cinemaworld", ("1", null));
        Save("filmworld", ("1", null));

        Assert.Equal(OffersStatus.Unavailable, _service.GetOffers("1").Status);
    }

    [Fact]
    public void GetProviderStatuses_ReportsFreshnessAndData()
    {
        Save("cinemaworld", ("1", 30m));

        var statuses = _service.GetProviderStatuses();

        Assert.Equal(new ProviderStatus("cinemaworld", true, true), statuses[0]);
        Assert.Equal(new ProviderStatus("filmworld", false, false), statuses[1]);
    }
}
