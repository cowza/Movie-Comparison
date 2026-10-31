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
        var client = services.AddHttpClient(provider.Name, (serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<ExternalApiSettings>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl);
                client.DefaultRequestHeaders.Add("x-access-token", settings.ApiToken);
            })
            // A followed redirect would carry the token header to wherever it points.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

        client.AddStandardResilienceHandler()
            .Configure((options, serviceProvider) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<ProviderResilienceSettings>>().Value;
                ApplyResilience(options, settings);
            });

        // Added after the resilience handler so it runs inside it.
        client.AddHttpMessageHandler(() => new BufferResponseBodyHandler());

        services.AddSingleton<IExternalMovieProvider>(serviceProvider => new HttpMovieProvider(
            provider,
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            serviceProvider.GetRequiredService<ILogger<HttpMovieProvider>>()));
    }

    public static void ApplyResilience(HttpStandardResilienceOptions options, ProviderResilienceSettings settings)
    {
        options.AttemptTimeout.Timeout = settings.AttemptTimeout;
        options.TotalRequestTimeout.Timeout = settings.TotalTimeout;
        options.Retry.MaxRetryAttempts = settings.MaxRetries;
        options.Retry.Delay = settings.RetryDelay;
        options.CircuitBreaker.MinimumThroughput = settings.CircuitMinimumThroughput;
        options.CircuitBreaker.FailureRatio = settings.CircuitFailureRatio;
        options.CircuitBreaker.BreakDuration = settings.CircuitBreakDuration;

        // A provider that hangs uses a full attempt timeout per failure, so the window has to fit
        // enough of them to trip the breaker. This also covers the library's minimum of two.
        var minimumSampling = settings.AttemptTimeout * (settings.CircuitMinimumThroughput + 1);
        if (options.CircuitBreaker.SamplingDuration < minimumSampling)
        {
            options.CircuitBreaker.SamplingDuration = minimumSampling;
        }
    }
}
