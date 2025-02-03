using Microsoft.AspNetCore.Mvc;
using MovieComparison.Core.DTOs;
using MovieComparison.Core.Interfaces;

namespace MovieComparison.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MoviesController : ControllerBase
    {
        private readonly IMovieService _movieService;
        private readonly ILogger<MoviesController> _logger;

        public MoviesController(
            IMovieService movieService,
            ILogger<MoviesController> logger)
        {
            _movieService = movieService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MovieDto>>> GetMovies()
        {
            try
            {
                var movies = await _movieService.GetAllMoviesAsync();
                return Ok(movies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving movies");
                return StatusCode(500, "An error occurred while retrieving movies");
            }
        }

        [HttpPost("prices")]
        public async Task<ActionResult<MoviePriceDto>> GetMoviePrices(
            [FromQuery] string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("No id specified");
            }

            try
            {
                var moviePrice = await _movieService.GetMovieBestPriceAsync(id);
                return Ok(moviePrice);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Unable to retrieve prices");
                return NotFound("No prices available for the specified providers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving movie prices");
                return StatusCode(500, "An error occurred while retrieving movie prices");
            }
        }
    }
}