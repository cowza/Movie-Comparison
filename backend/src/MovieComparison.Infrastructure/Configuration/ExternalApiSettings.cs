using System.ComponentModel.DataAnnotations;

namespace MovieComparison.Infrastructure.Configuration;

public sealed class ProviderSettings
{
    public string Name { get; set; } = "";
    public string IdPrefix { get; set; } = "";
}

public sealed class ExternalApiSettings
{
    public const string SectionName = "ExternalApiSettings";

    [Required, Url]
    public string BaseUrl { get; set; } = "";

    [Required]
    public string ApiToken { get; set; } = "";

    [MinLength(1)]
    public List<ProviderSettings> Providers { get; set; } = [];
}

public sealed class SnapshotSettings
{
    public const string SectionName = "Snapshot";

    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    // A failed cycle retries sooner so a slow cold start doesn't leave a provider missing for a full interval.
    public TimeSpan FailureRetryInterval { get; set; } = TimeSpan.FromSeconds(30);

    // Older than this and a price is still shown, but flagged as stale.
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(15);

    // Older than this and data is dropped. Prices change daily, so a day old is the most we can stand behind.
    public TimeSpan MaxDataAge { get; set; } = TimeSpan.FromHours(24);
}

public sealed class ProviderResilienceSettings
{
    public const string SectionName = "ProviderResilience";

    // The upstream is hosted on a plan that cold starts, so the first call can take well over 10 seconds.
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    // The library default of 100 calls per sample would never trip at background refresh volume.
    public int CircuitMinimumThroughput { get; set; } = 4;
    public double CircuitFailureRatio { get; set; } = 0.5;
    public TimeSpan CircuitBreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
