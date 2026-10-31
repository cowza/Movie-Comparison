using MovieComparison.Core.Exceptions;
using MovieComparison.Core.Interfaces;
using MovieComparison.Core.Models;

namespace MoveComparison.UnitTests.Snapshots;

internal sealed class StubProvider(string name) : IExternalMovieProvider
{
    public string ProviderName => name;
    public Func<IReadOnlyList<ProviderMovie>> Movies { get; set; } = () => [];
    public Func<string, decimal> Price { get; set; } = _ => 10m;

    public Task<IReadOnlyList<ProviderMovie>> GetMoviesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Movies());

    public Task<decimal> GetPriceAsync(string movieId, CancellationToken cancellationToken) =>
        Task.FromResult(Price(movieId));

    public static IReadOnlyList<ProviderMovie> Fails() => throw new ProviderException("down");
}
