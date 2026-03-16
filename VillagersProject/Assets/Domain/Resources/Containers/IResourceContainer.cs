using System.Collections.Generic;

public interface IResourceContainer
{
    int GetAmount(string resourceId);
    bool Has(string resourceId, int amount);
    void Add(string resourceId, int amount);
    bool TrySpend(string resourceId, int amount);
    void Clear();

    IReadOnlyDictionary<string, int> ReadOnlySnapshot();
    Dictionary<string, int> Snapshot();
    List<ResourceStack> GetStacks();
}