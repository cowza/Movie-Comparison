using MovieComparison.Core.DTOs;

namespace MovieComparison.Core.Interfaces;

public interface IMovieService
{
    /// <summary>Returns null when no provider has usable data yet.</summary>
    MovieListDto? GetMovies();
    OffersResult GetOffers(string id);
    IReadOnlyList<ProviderStatus> GetProviderStatuses();
}
