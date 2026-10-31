using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieComparison.Core.Interfaces;
using MovieComparison.Infrastructure.Configuration;

namespace MovieComparison.Infrastructure.Snapshots;

/// <summary>Refreshes each provider on its own loop so one hanging provider can't delay another.</summary>
public sealed class SnapshotRefreshWorker(
    IEnumerable<IExternalMovieProvider> providers,
    ProviderRefresher refresher,
    IOptions<SnapshotSettings> settings,
    TimeProvider timeProvider) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(providers.Select(provider => RunAsync(provider, stoppingToken)));

    private async Task RunAsync(IExternalMovieProvider provider, CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var refreshed = await refresher.RefreshAsync(provider, stoppingToken);
                var delay = refreshed ? settings.Value.RefreshInterval : settings.Value.FailureRetryInterval;
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
