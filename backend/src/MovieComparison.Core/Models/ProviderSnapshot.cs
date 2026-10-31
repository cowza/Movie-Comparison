namespace MovieComparison.Core.Models;

public sealed record ProviderMovie(string Id, string Title, string Year, string? Poster);

public sealed record ProviderOffer(decimal Price, DateTimeOffset AsOf);

public sealed record SnapshotMovie(ProviderMovie Movie, ProviderOffer? Offer);

/// <summary>The last good data held for one provider. Failed refreshes never replace it.</summary>
public sealed record ProviderSnapshot(
    string Provider,
    IReadOnlyDictionary<string, SnapshotMovie> Movies,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt)
{
    public static ProviderSnapshot Empty(string provider) =>
        new(provider, new Dictionary<string, SnapshotMovie>(), null, null);
}
