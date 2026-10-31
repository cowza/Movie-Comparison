using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using MovieComparison.Core.Interfaces;
using MovieComparison.Core.Models;

namespace MovieComparison.Infrastructure.Snapshots;

/// <summary>Runs one refresh cycle for a provider. This is the only code that calls providers.</summary>
public sealed class ProviderRefresher(
    ISnapshotStore store,
    TimeProvider timeProvider,
    ILogger<ProviderRefresher> logger)
{
    public const string MeterName = "MovieComparison.Snapshots";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Refreshes = Meter.CreateCounter<long>("movie_comparison.provider.refreshes");

    /// <returns>True when the cycle produced usable data. False means it's worth retrying soon.</returns>
    public async Task<bool> RefreshAsync(IExternalMovieProvider provider, CancellationToken cancellationToken)
    {
        var name = provider.ProviderName;
        var previous = store.Get(name) ?? ProviderSnapshot.Empty(name);

        IReadOnlyList<ProviderMovie> listing;
        try
        {
            listing = await provider.GetMoviesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
        {
            logger.LogWarning(ex, "Keeping last good snapshot for {Provider}: catalogue refresh failed", name);
            return RecordFailure(previous);
        }

        var movies = new Dictionary<string, SnapshotMovie>();
        var priced = 0;

        foreach (var movie in listing)
        {
            ProviderOffer? offer;
            try
            {
                offer = new ProviderOffer(await provider.GetPriceAsync(movie.Id, cancellationToken), timeProvider.GetUtcNow());
                priced++;
            }
            catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
            {
                logger.LogWarning(ex, "Keeping last good price for {Provider} movie {MovieId}", name, movie.Id);
                offer = previous.Movies.GetValueOrDefault(movie.Id)?.Offer;
            }

            movies[movie.Id] = new SnapshotMovie(movie, offer);
        }

        // A catalogue with no prices can't answer anything, so it isn't a successful refresh.
        if (priced == 0)
        {
            logger.LogWarning("Keeping last good snapshot for {Provider}: no prices refreshed", name);
            return RecordFailure(previous);
        }

        var allPriced = priced == listing.Count;
        var now = timeProvider.GetUtcNow();
        store.Save(previous with
        {
            Movies = movies,
            LastSuccessAt = now,
            LastFailureAt = allPriced ? previous.LastFailureAt : now,
        });

        RecordOutcome(name, allPriced ? "refreshed" : "partial");
        return true;
    }

    private bool RecordFailure(ProviderSnapshot previous)
    {
        store.Save(previous with { LastFailureAt = timeProvider.GetUtcNow() });
        RecordOutcome(previous.Provider, "failed");
        return false;
    }

    private static void RecordOutcome(string provider, string outcome) =>
        Refreshes.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("outcome", outcome));

    private static bool IsProviderFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested;
}
