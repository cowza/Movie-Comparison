using Microsoft.Extensions.Diagnostics.HealthChecks;
using MovieComparison.Core.Interfaces;

namespace MovieComparison.Api;

/// <summary>
/// Stale data still counts as ready: pulling the instance during a provider outage
/// would throw away the last good prices it exists to serve.
/// </summary>
public sealed class SnapshotHealthCheck(IMovieService movieService) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var statuses = movieService.GetProviderStatuses();

        var result = statuses.All(s => s.Fresh)
            ? HealthCheckResult.Healthy()
            : statuses.Any(s => s.HasData)
                ? HealthCheckResult.Degraded($"Not fresh: {string.Join(", ", statuses.Where(s => !s.Fresh).Select(s => s.Provider))}")
                : HealthCheckResult.Unhealthy("No provider data yet");

        return Task.FromResult(result);
    }
}
