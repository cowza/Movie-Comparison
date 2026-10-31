using MovieComparison.Core.Models;

namespace MovieComparison.Core.Interfaces;

public interface ISnapshotStore
{
    ProviderSnapshot? Get(string provider);
    void Save(ProviderSnapshot snapshot);
}
