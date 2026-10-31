using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieComparison.Core.Interfaces;
using MovieComparison.Infrastructure.Configuration;
using MovieComparison.Infrastructure.Services;
using MovieComparison.Infrastructure.Snapshots;

namespace MovieComparison.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMovieSnapshots(this IServiceCollection services, IConfiguration configuration)
    {
        var apiSection = configuration.GetSection(ExternalApiSettings.SectionName);

        services.AddOptions<ExternalApiSettings>()
            .Bind(apiSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<SnapshotSettings>().Bind(configuration.GetSection(SnapshotSettings.SectionName));
        services.AddOptions<ProviderResilienceSettings>().Bind(configuration.GetSection(ProviderResilienceSettings.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISnapshotStore, InMemorySnapshotStore>();
        services.AddSingleton<ProviderRefresher>();
        services.AddSingleton<IMovieService, MovieService>();
        services.AddHostedService<SnapshotRefreshWorker>();

        foreach (var provider in apiSection.GetSection("Providers").Get<List<ProviderSettings>>() ?? [])
        {
            AddProvider(services, provider);
        }

        return services;
    }

    private static void AddProvider(IServiceCollection services, ProviderSettings provider)
    {
        // Each provider needs its own client name. Sharing one stacks every provider's handlers
        // onto both clients, and shares a circuit breaker between them.
        services.AddHttpClient(provider.Name, (serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<ExternalApiSettings>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl);
                client.DefaultRequestHeaders.Add("x-access-token", settings.ApiToken);
            })
            .AddStandardResilienceHandler()
            .Configure((options, serviceProvider) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<ProviderResilienceSettings>>().Value;
                ApplyResilience(options, settings);
            });

        services.AddSingleton<IExternalMovieProvider>(serviceProvider => new HttpMovieProvider(
            provider,
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            serviceProvider.GetRequiredService<ILogger<HttpMovieProvider>>()));
    }

    private static void ApplyResilience(HttpStandardResilienceOptions options, ProviderResilienceSettings settings)
    {
        options.AttemptTimeout.Timeout = settings.AttemptTimeout;
        options.TotalRequestTimeout.Timeout = settings.TotalTimeout;
        options.Retry.MaxRetryAttempts = settings.MaxRetries;
        options.Retry.Delay = settings.RetryDelay;
        options.CircuitBreaker.MinimumThroughput = settings.CircuitMinimumThroughput;
        options.CircuitBreaker.FailureRatio = settings.CircuitFailureRatio;
        options.CircuitBreaker.BreakDuration = settings.CircuitBreakDuration;

        // The library requires the sampling window to span at least two attempt timeouts.
        var minimumSampling = settings.AttemptTimeout * 2;
        if (options.CircuitBreaker.SamplingDuration < minimumSampling)
        {
            options.CircuitBreaker.SamplingDuration = minimumSampling;
        }
    }
}
