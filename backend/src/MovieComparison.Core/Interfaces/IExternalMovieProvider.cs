using MovieComparison.Core.Models;

namespace MovieComparison.Core.Interfaces;

public interface IExternalMovieProvider
{
    string ProviderName { get; }
    Task<IReadOnlyList<ProviderMovie>> GetMoviesAsync(CancellationToken cancellationToken);
    Task<decimal> GetPriceAsync(string movieId, CancellationToken cancellationToken);
}
