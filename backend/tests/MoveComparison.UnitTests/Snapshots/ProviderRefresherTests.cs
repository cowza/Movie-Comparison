using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MovieComparison.Core.Exceptions;
using MovieComparison.Core.Models;
using MovieComparison.Infrastructure.Snapshots;

namespace MoveComparison.UnitTests.Snapshots;

public class ProviderRefresherTests
{
    private static readonly ProviderMovie MovieA = new("1", "A", "1977", null);
    private static readonly ProviderMovie MovieB = new("2", "B", "1980", null);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly InMemorySnapshotStore _store = new();
    private readonly ProviderRefresher _refresher;
    private readonly StubProvider _provider = new("filmworld") { Movies = () => [MovieA, MovieB] };

    public ProviderRefresherTests()
    {
        _refresher = new ProviderRefresher(_store, _time, NullLogger<ProviderRefresher>.Instance);
    }

    private ProviderSnapshot Snapshot => _store.Get("filmworld")!;

    [Fact]
    public async Task Refresh_StoresEveryMovieAndPrice()
    {
        var refreshed = await _refresher.RefreshAsync(_provider, CancellationToken.None);

        Assert.True(refreshed);
        Assert.Equal(2, Snapshot.Movies.Count);
        Assert.Equal(10m, Snapshot.Movies["1"].Offer!.Price);
        Assert.Equal(_time.GetUtcNow(), Snapshot.LastSuccessAt);
    }

    [Fact]
    public async Task Refresh_WhenCatalogueFails_KeepsMoviesAndRecordsTheFailure()
    {
        await _refresher.RefreshAsync(_provider, CancellationToken.None);
        var successAt = Snapshot.LastSuccessAt;
        _time.Advance(TimeSpan.FromMinutes(5));
        _provider.Movies = StubProvider.Fails;

        var refreshed = await _refresher.RefreshAsync(_provider, CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal(2, Snapshot.Movies.Count);
        Assert.Equal(successAt, Snapshot.LastSuccessAt);
        Assert.Equal(_time.GetUtcNow(), Snapshot.LastFailureAt);
    }

    [Fact]
    public async Task Refresh_WhenOnePriceFails_KeepsThatPriceAtItsOldTimestamp()
    {
        await _refresher.RefreshAsync(_provider, CancellationToken.None);
        var firstAsOf = Snapshot.Movies["1"].Offer!.AsOf;
        _time.Advance(TimeSpan.FromMinutes(5));
        _provider.Price = id => id == "1" ? throw new ProviderException("down") : 12m;

        var refreshed = await _refresher.RefreshAsync(_provider, CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal(new ProviderOffer(10m, firstAsOf), Snapshot.Movies["1"].Offer);
        Assert.Equal(12m, Snapshot.Movies["2"].Offer!.Price);
    }

    [Fact]
    public async Task Refresh_WhenPriceFailsWithNoHistory_ListsTheMovieWithoutAnOffer()
    {
        _provider.Price = _ => throw new ProviderException("down");

        await _refresher.RefreshAsync(_provider, CancellationToken.None);

        Assert.Null(Snapshot.Movies["1"].Offer);
    }

    [Fact]
    public async Task Refresh_WhenAMovieLeavesTheCatalogue_DropsIt()
    {
        await _refresher.RefreshAsync(_provider, CancellationToken.None);
        _provider.Movies = () => [MovieA];

        await _refresher.RefreshAsync(_provider, CancellationToken.None);

        Assert.False(Snapshot.Movies.ContainsKey("2"));
    }

    [Fact]
    public async Task Refresh_WhenStopping_PropagatesCancellation()
    {
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        _provider.Movies = () => throw new OperationCanceledException(stopping.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => _refresher.RefreshAsync(_provider, stopping.Token));
    }
}
