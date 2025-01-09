using MovieComparison.Core.Models;

namespace MovieComparison.Core.Interfaces
{
    public interface IExternalMovieProvider
    {
        string ProviderName { get; }
        Task<IEnumerable<Movie>> GetMoviesAsync();
        Task<MovieDetails> GetMovieDetailsAsync(string movieId);
    }
}