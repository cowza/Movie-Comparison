using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieComparison.Core.Exceptions;
using MovieComparison.Core.Interfaces;
using MovieComparison.Core.Models;
using MovieComparison.Infrastructure.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

namespace MovieComparison.Infrastructure.Services
{
    public class FilmWorldProvider : IExternalMovieProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FilmWorldProvider> _logger;
        private readonly ExternalApiSettings _settings;

        public string ProviderName => "filmworld";
        public string ProviderIDPrefix => "fw";

        public FilmWorldProvider(
            HttpClient httpClient,
            IOptions<ExternalApiSettings> settings,
            ILogger<FilmWorldProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value;

            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("x-access-token", _settings.ApiToken);
        }

        public async Task<IEnumerable<Movie>> GetMoviesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/{ProviderName}/movies");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadFromJsonAsync<ExternalMoviesListResponse>();

                return content.Movies.Select(m => new Movie
                {
                    ID = m.ID,
                    Title = m.Title,
                    Year = m.Year,
                    Poster = m.Poster,
                    Provider = ProviderName,
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error fetching movies from FilmWorld: {Message}", ex.Message);
                throw new ProviderException("FilmWorld service unavailable", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing FilmWorld response");
                throw new ProviderException("Invalid response from FilmWorld", ex);
            }
        }

        public async Task<MovieDetails> GetMovieDetailsAsync(string movieId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/{ProviderName}/movie/{ProviderIDPrefix + movieId}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadFromJsonAsync<ExternalMovieDetailsResponse>();

                return new MovieDetails
                {
                    ID = content.ID,
                    Title = content.Title,
                    Year = content.Year,
                    Type = content.Type,
                    Poster = content.Poster,
                    Price = content.Price,
                    Provider = ProviderName,
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error fetching movie details from FilmWorld: {Message}", ex.Message);
                throw new ProviderException("FilmWorld service unavailable", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing FilmWorld movie details");
                throw new ProviderException("Invalid response from FilmWorld", ex);
            }
        }
    }
}