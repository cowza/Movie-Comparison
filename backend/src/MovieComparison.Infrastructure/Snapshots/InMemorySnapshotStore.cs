using System.Collections.Concurrent;
using MovieComparison.Core.Interfaces;
using MovieComparison.Core.Models;

namespace MovieComparison.Infrastructure.Snapshots;

public sealed class InMemorySnapshotStore : ISnapshotStore
{
    private readonly ConcurrentDictionary<string, ProviderSnapshot> _snapshots = new();

    public ProviderSnapshot? Get(string provider) => _snapshots.GetValueOrDefault(provider);

    public void Save(ProviderSnapshot snapshot) => _snapshots[snapshot.Provider] = snapshot;
}
