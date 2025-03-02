using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MovieComparison.Core.DTOs;
using MovieComparison.Core.Interfaces;
using MovieComparison.Core.Models;

namespace MovieComparison.Infrastructure.Services;

public class MovieService : IMovieService
{
    private readonly IEnumerable<IExternalMovieProvider> _providers;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MovieService> _logger;
    private const int CACHE_DURATION_MINUTES = 5;

    public MovieService(
        IEnumerable<IExternalMovieProvider> providers,
        IMemoryCache cache,
        ILogger<MovieService> logger)
    {
        _providers = providers;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<MovieDto>> GetAllMoviesAsync()
    {
        const string cacheKey = "all_movies";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out IEnumerable<MovieDto> cachedMovies))
        {
            _logger.LogInformation("Returning movies from cache");
            return cachedMovies; 
        }

        // Fetch from all providers concurrently
        var providerTasks = _providers.Select(async provider =>
        {
            try
            {
                var movies = await provider.GetMoviesAsync();
                return (Success: true, Movies: movies, Provider: provider.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch movies from provider {Provider}", provider.ProviderName);
                return (Success: false, Movies: Enumerable.Empty<Movie>(), Provider: provider.ProviderName);
            }
        });

        var results = await Task.WhenAll(providerTasks);

        // Combine all successful results
        var allMovies = results
            .Where(r => r.Success)
            .SelectMany(r => r.Movies)
            .ToList();

        if (!allMovies.Any())
        {
            _logger.LogWarning("No movies retrieved from any provider");
            return Enumerable.Empty<MovieDto>();
        }

        // Group by title to handle duplicates across providers
        var groupedMovies = allMovies
            .GroupBy(m => m.Title)
            .Select(g => new MovieDto
            {
                Title = g.First().Title,
                Year = g.First().Year,
                Poster = g.First().Poster,
                ID = g.First().ID.Substring(2),
                Providers = string.Join(";", g.Select(m => m.Provider))
            })
            .ToList();

        // Cache the results
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

        _cache.Set(cacheKey, groupedMovies, cacheOptions);

        return groupedMovies;
    }

    public async Task<MoviePriceDto> GetMovieBestPriceAsync(string id)
    {
        var cacheKey = $"movie_prices_{id}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out MoviePriceDto cachedDetails))
        {
            _logger.LogInformation("Returning cached prices for providers");
            return cachedDetails;
        }

        // Fetch prices from each provider concurrently
        var priceTasks = _providers.Select(async provider =>
        {
            try
            {
                var details = await provider.GetMovieDetailsAsync(id);
                return (Success: true, Details: details, Provider: provider.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch movie details from provider {Provider} for ID {MovieId}",
                    provider.ProviderName, id);
                return (Success: false, Details: (MovieDetails)null, Provider: provider.ProviderName);
            }
        });

        var results = await Task.WhenAll(priceTasks);

        // Get successful results
        var validResults = results
            .Where(r => r.Success && r.Details != null)
            .ToList();

        if (!validResults.Any())
        {
            _logger.LogWarning("No prices retrieved from any provider");
            throw new InvalidOperationException("Unable to retrieve prices from any provider");
        }

        // Find the best price
        var bestPriceResult = validResults
            .MinBy(r => decimal.TryParse(r.Details.Price, out var price) ? price : decimal.MaxValue);

        var moviePriceDto = new MoviePriceDto
        {
            Provider = bestPriceResult.Provider,
            Price = bestPriceResult.Details.Price
        };

        // Only cache if we got successful responses from all requested providers
        if (validResults.Count == _providers.Count())
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            _cache.Set(cacheKey, moviePriceDto, cacheOptions);
            _logger.LogInformation("Cached prices for providers: {ProviderId}", id);
        }

        return moviePriceDto;
    }
}