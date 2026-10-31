using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MovieComparison.Core.Exceptions;
using MovieComparison.Core.Interfaces;
using MovieComparison.Core.Models;
using MovieComparison.Infrastructure.Configuration;
using Polly;

namespace MovieComparison.Infrastructure.Services;

/// <summary>Every provider shares one API shape, so adding one is a configuration change.</summary>
public sealed class HttpMovieProvider(
    ProviderSettings settings,
    IHttpClientFactory clientFactory,
    ILogger<HttpMovieProvider> logger) : IExternalMovieProvider
{
    public string ProviderName => settings.Name;

    public async Task<IReadOnlyList<ProviderMovie>> GetMoviesAsync(CancellationToken cancellationToken)
    {
        var content = await GetAsync<ExternalMoviesListResponse>($"/api/{ProviderName}/movies", cancellationToken);

        var movies = new List<ProviderMovie>();
        foreach (var movie in content.Movies ?? [])
        {
            if (!movie.ID.StartsWith(settings.IdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Skipping {Provider} movie with unexpected ID {MovieId}", ProviderName, movie.ID);
                continue;
            }

            movies.Add(new ProviderMovie(movie.ID[settings.IdPrefix.Length..], movie.Title, movie.Year, movie.Poster));
        }

        // An empty catalogue from a flaky API is far more likely to be a fault than the truth,
        // and treating it as real would wipe the last good snapshot.
        return movies.Count > 0 ? movies : throw new ProviderException($"{ProviderName} returned no movies");
    }

    public async Task<decimal> GetPriceAsync(string movieId, CancellationToken cancellationToken)
    {
        var path = $"/api/{ProviderName}/movie/{Uri.EscapeDataString(settings.IdPrefix + movieId)}";
        var content = await GetAsync<ExternalMovieDetailsResponse>(path, cancellationToken);

        if (!decimal.TryParse(content.Price, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
        {
            throw new ProviderException($"{ProviderName} returned an invalid price for {movieId}");
        }

        return price;
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            var client = clientFactory.CreateClient(ProviderName);
            using var response = await client.GetAsync(path, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new ProviderException($"{ProviderName} returned an empty response");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException or ExecutionRejectedException)
        {
            throw new ProviderException($"{ProviderName} request failed: {ex.Message}", ex);
        }
    }
}
