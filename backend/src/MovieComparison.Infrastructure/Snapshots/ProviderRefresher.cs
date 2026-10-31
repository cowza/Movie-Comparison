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

    /// <returns>True when the catalogue and every price were refreshed.</returns>
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
            store.Save(previous with { LastFailureAt = timeProvider.GetUtcNow() });
            Refreshes.Add(1, new KeyValuePair<string, object?>("provider", name), new KeyValuePair<string, object?>("outcome", "failed"));
            return false;
        }

        var movies = new Dictionary<string, SnapshotMovie>();
        var allPriced = true;

        foreach (var movie in listing)
        {
            ProviderOffer? offer;
            try
            {
                offer = new ProviderOffer(await provider.GetPriceAsync(movie.Id, cancellationToken), timeProvider.GetUtcNow());
            }
            catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
            {
                logger.LogWarning(ex, "Keeping last good price for {Provider} movie {MovieId}", name, movie.Id);
                offer = previous.Movies.GetValueOrDefault(movie.Id)?.Offer;
                allPriced = false;
            }

            movies[movie.Id] = new SnapshotMovie(movie, offer);
        }

        var now = timeProvider.GetUtcNow();
        store.Save(previous with
        {
            Movies = movies,
            LastSuccessAt = now,
            LastFailureAt = allPriced ? previous.LastFailureAt : now,
        });

        Refreshes.Add(1, new KeyValuePair<string, object?>("provider", name), new KeyValuePair<string, object?>("outcome", allPriced ? "refreshed" : "partial"));
        return allPriced;
    }

    private static bool IsProviderFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested;
}
