using MovieComparison.Core.DTOs;

namespace MovieComparison.Core.Interfaces
{
    public interface IMovieService
    {
        Task<IEnumerable<MovieDto>> GetAllMoviesAsync();
        Task<MoviePriceDto> GetMovieBestPriceAsync(IEnumerable<ProviderDto> providers);
    }
}