using Microsoft.Extensions.Http.Resilience;
using MovieComparison.Infrastructure;
using MovieComparison.Infrastructure.Configuration;

namespace MoveComparison.UnitTests.Providers;

public class ProviderResilienceTests
{
    [Fact]
    public void DefaultSettings_LetTimeoutsAloneTripTheBreaker()
    {
        var settings = new ProviderResilienceSettings();
        var options = new HttpStandardResilienceOptions();

        ServiceCollectionExtensions.ApplyResilience(options, settings);

        // Each timed out attempt uses a full attempt timeout, so the window must fit enough of them.
        var timeToTrip = settings.AttemptTimeout * settings.CircuitMinimumThroughput;
        Assert.True(options.CircuitBreaker.SamplingDuration > timeToTrip);
    }
}
