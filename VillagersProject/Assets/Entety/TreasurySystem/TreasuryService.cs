using System.Collections.Generic;
using UnityEngine;

public class TreasuryService
{
    private readonly Dictionary<string, int> storage = new();

    public int GetAmount(string resourceId)
        => storage.TryGetValue(resourceId, out var v) ? v : 0;

    public void Add(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
            return;

        storage.TryGetValue(resourceId, out var cur);
        storage[resourceId] = cur + amount;

        Debug.Log($"[Treasury] +{amount} {resourceId} (total={storage[resourceId]})");
    }
}

