using Microsoft.Extensions.Options;
using MovieComparison.Core.DTOs;
using MovieComparison.Core.Interfaces;
using MovieComparison.Core.Models;
using MovieComparison.Infrastructure.Configuration;

namespace MovieComparison.Infrastructure.Services;

/// <summary>Answers requests from snapshots alone, so a request never waits on a provider.</summary>
public sealed class MovieService(
    IEnumerable<IExternalMovieProvider> providers,
    ISnapshotStore store,
    IOptions<SnapshotSettings> settings,
    TimeProvider timeProvider) : IMovieService
{
    private readonly IReadOnlyList<string> _providerNames = providers.Select(p => p.ProviderName).ToList();
    private readonly SnapshotSettings _settings = settings.Value;

    public MovieListDto? GetMovies()
    {
        var now = timeProvider.GetUtcNow();
        var snapshots = _providerNames
            .Select(store.Get)
            .OfType<ProviderSnapshot>()
            .Where(s => HasData(s, now))
            .ToList();

        if (snapshots.Count == 0)
        {
            return null;
        }

        var movies = snapshots
            .SelectMany(s => s.Movies.Values.Select(m => (s.Provider, m.Movie)))
            .GroupBy(entry => entry.Movie.Id)
            .Select(group =>
            {
                var movie = group.First().Movie;
                return new MovieDto(movie.Id, movie.Title, movie.Year, movie.Poster, group.Select(e => e.Provider).ToList());
            })
            .OrderBy(movie => movie.Title)
            .ToList();

        var unavailable = _providerNames.Where(name => !IsFresh(store.Get(name), now)).ToList();
        return new MovieListDto(movies, unavailable.Count == 0, unavailable);
    }

    public OffersResult GetOffers(string id)
    {
        var now = timeProvider.GetUtcNow();
        var offers = new List<OfferDto>();
        var unavailable = new List<string>();
        ProviderMovie? movie = null;

        foreach (var name in _providerNames)
        {
            var snapshot = store.Get(name);
            var entry = snapshot is not null && HasData(snapshot, now) ? snapshot.Movies.GetValueOrDefault(id) : null;

            if (entry is null)
            {
                // A fresh catalogue without this movie means the provider doesn't sell it.
                // Without one we can't tell, so the result can't claim to be complete.
                if (!IsFresh(snapshot, now))
                {
                    unavailable.Add(name);
                }
                continue;
            }

            movie ??= entry.Movie;

            if (entry.Offer is { } offer && now - offer.AsOf <= _settings.MaxDataAge)
            {
                var stale = now - offer.AsOf > _settings.StaleAfter;
                offers.Add(new OfferDto(name, offer.Price, offer.AsOf, stale));
                if (stale)
                {
                    unavailable.Add(name);
                }
            }
            else
            {
                unavailable.Add(name);
            }
        }

        if (movie is null)
        {
            return new OffersResult(unavailable.Count > 0 ? OffersStatus.Unavailable : OffersStatus.NotFound);
        }

        if (offers.Count == 0)
        {
            return new OffersResult(OffersStatus.Unavailable);
        }

        var ordered = offers.OrderBy(o => o.Price).ToList();
        return new OffersResult(OffersStatus.Found, new MovieOffersDto(
            movie.Id, movie.Title, movie.Year, movie.Poster, ordered[0], ordered, unavailable.Count == 0, unavailable));
    }

    public IReadOnlyList<ProviderStatus> GetProviderStatuses()
    {
        var now = timeProvider.GetUtcNow();
        return _providerNames
            .Select(name => store.Get(name) is { } snapshot
                ? new ProviderStatus(name, IsFresh(snapshot, now), HasData(snapshot, now))
                : new ProviderStatus(name, false, false))
            .ToList();
    }

    private bool IsFresh(ProviderSnapshot? snapshot, DateTimeOffset now) =>
        snapshot?.LastSuccessAt is { } at && now - at <= _settings.StaleAfter;

    private bool HasData(ProviderSnapshot snapshot, DateTimeOffset now) =>
        snapshot.LastSuccessAt is { } at && now - at <= _settings.MaxDataAge;
}
