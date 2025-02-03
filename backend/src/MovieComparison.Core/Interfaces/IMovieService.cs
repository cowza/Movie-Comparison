using MovieComparison.Core.DTOs;

namespace MovieComparison.Core.Interfaces
{
    public interface IMovieService
    {
        Task<IEnumerable<MovieDto>> GetAllMoviesAsync();
        Task<MoviePriceDto> GetMovieBestPriceAsync(string id);
    }
}