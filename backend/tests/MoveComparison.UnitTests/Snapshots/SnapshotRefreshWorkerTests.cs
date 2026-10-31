using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MovieComparison.Core.Exceptions;
using MovieComparison.Core.Models;
using MovieComparison.Infrastructure.Configuration;
using MovieComparison.Infrastructure.Snapshots;

namespace MoveComparison.UnitTests.Snapshots;

public class SnapshotRefreshWorkerTests
{
    private static readonly SnapshotSettings Settings = new()
    {
        RefreshInterval = TimeSpan.FromMinutes(5),
        FailureRetryInterval = TimeSpan.FromSeconds(30),
    };

    private readonly FakeTimeProvider _time = new();
    private int _calls;

    private SnapshotRefreshWorker Worker(Func<IReadOnlyList<ProviderMovie>> movies, Func<string, decimal>? price = null)
    {
        var provider = new StubProvider("filmworld")
        {
            Movies = () =>
            {
                Interlocked.Increment(ref _calls);
                return movies();
            },
            Price = price ?? (_ => 10m),
        };
        var refresher = new ProviderRefresher(new InMemorySnapshotStore(), _time, NullLogger<ProviderRefresher>.Instance);
        return new SnapshotRefreshWorker([provider], refresher, Options.Create(Settings), _time);
    }

    private async Task WaitForCallsAsync(int expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref _calls) < expected)
        {
            Assert.True(DateTime.UtcNow < deadline, $"Expected {expected} calls, saw {_calls}");
            await Task.Delay(5);
        }

        // Gives the loop time to register its next delay before the test moves the clock.
        await Task.Delay(50);
    }

    [Fact]
    public async Task AfterASuccessfulCycle_WaitsTheFullInterval()
    {
        using var worker = Worker(() => [new ProviderMovie("1", "A", "1977", null)]);
        await worker.StartAsync(CancellationToken.None);
        await WaitForCallsAsync(1);

        _time.Advance(Settings.FailureRetryInterval);
        await Task.Delay(50);
        Assert.Equal(1, Volatile.Read(ref _calls));

        _time.Advance(Settings.RefreshInterval);
        await WaitForCallsAsync(2);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AfterAFailedCycle_RetriesSooner()
    {
        using var worker = Worker(StubProvider.Fails);
        await worker.StartAsync(CancellationToken.None);
        await WaitForCallsAsync(1);

        _time.Advance(Settings.FailureRetryInterval);

        await WaitForCallsAsync(2);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OnePermanentlyBadPrice_DoesNotShortenTheInterval()
    {
        using var worker = Worker(
            () => [new ProviderMovie("1", "A", "1977", null), new ProviderMovie("2", "B", "1980", null)],
            id => id == "1" ? throw new ProviderException("bad price") : 10m);
        await worker.StartAsync(CancellationToken.None);
        await WaitForCallsAsync(1);

        _time.Advance(Settings.FailureRetryInterval);
        await Task.Delay(50);

        Assert.Equal(1, Volatile.Read(ref _calls));
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stopping_EndsTheLoopCleanly()
    {
        using var worker = Worker(StubProvider.Fails);
        await worker.StartAsync(CancellationToken.None);
        await WaitForCallsAsync(1);

        await worker.StopAsync(CancellationToken.None);

        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }
}
