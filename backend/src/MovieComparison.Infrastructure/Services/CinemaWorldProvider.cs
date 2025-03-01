using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieComparison.Core.Exceptions;
using MovieComparison.Core.Interfaces;
using MovieComparison.Core.Models;
using MovieComparison.Infrastructure.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

public class CinemaWorldProvider : IExternalMovieProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CinemaWorldProvider> _logger;
    private readonly ExternalApiSettings _settings;

    public string ProviderName => "cinemaworld";
    public string ProviderIDPrefix => "cw";

    public CinemaWorldProvider(
        HttpClient httpClient,
        IOptions<ExternalApiSettings> settings,
        ILogger<CinemaWorldProvider> logger)
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
            _logger.LogError(ex, "Error fetching movies from CinemaWorld: {Message}", ex.Message);
            throw new ProviderException("CinemaWorld service unavailable", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing CinemaWorld response");
            throw new ProviderException("Invalid response from CinemaWorld", ex);
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
                Provider = ProviderName
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error fetching movie details from CinemaWorld: {Message}", ex.Message);
            throw new ProviderException("CinemaWorld service unavailable", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing CinemaWorld movie details");
            throw new ProviderException("Invalid response from CinemaWorld", ex);
        }
    }
}