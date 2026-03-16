using System.Collections.Generic;

public class ResourceService
{
    public void Add(IResourceContainer container, string resourceId, int amount)
    {
        if (container == null)
            return;

        container.Add(resourceId, amount);
    }

    public bool TrySpend(IResourceContainer container, string resourceId, int amount)
    {
        if (container == null)
            return false;

        return container.TrySpend(resourceId, amount);
    }

    public int GetAmount(IResourceContainer container, string resourceId)
    {
        if (container == null)
            return 0;

        return container.GetAmount(resourceId);
    }

    public void TransferAll(IResourceContainer from, IResourceContainer to)
    {
        if (from == null || to == null)
            return;

        var snapshot = from.Snapshot();
        foreach (var kv in snapshot)
        {
            if (kv.Value <= 0)
                continue;

            to.Add(kv.Key, kv.Value);
        }

        from.Clear();
    }

    public Dictionary<string, int> Snapshot(IResourceContainer container)
    {
        if (container == null)
            return new Dictionary<string, int>();

        return container.Snapshot();
    }
}