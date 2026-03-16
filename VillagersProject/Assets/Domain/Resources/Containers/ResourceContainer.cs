using System;
using System.Collections.Generic;

[Serializable]
public class ResourceContainer : IResourceContainer
{
    private readonly Dictionary<string, int> _storage =
        new(StringComparer.OrdinalIgnoreCase);

    public ResourceContainer()
    {
    }

    public ResourceContainer(Dictionary<string, int> initial)
    {
        LoadFrom(initial);
    }

    public int GetAmount(string resourceId)
    {
        resourceId = ResourceStack.Normalize(resourceId);
        if (string.IsNullOrWhiteSpace(resourceId))
            return 0;

        return _storage.TryGetValue(resourceId, out var amount) ? amount : 0;
    }

    public bool Has(string resourceId, int amount)
    {
        if (amount <= 0)
            return true;

        return GetAmount(resourceId) >= amount;
    }

    public void Add(string resourceId, int amount)
    {
        resourceId = ResourceStack.Normalize(resourceId);
        if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
            return;

        _storage.TryGetValue(resourceId, out var current);
        _storage[resourceId] = current + amount;
    }

    public bool TrySpend(string resourceId, int amount)
    {
        resourceId = ResourceStack.Normalize(resourceId);
        if (string.IsNullOrWhiteSpace(resourceId))
            return false;

        if (amount <= 0)
            return true;

        if (!_storage.TryGetValue(resourceId, out var current))
            return false;

        if (current < amount)
            return false;

        current -= amount;

        if (current <= 0)
            _storage.Remove(resourceId);
        else
            _storage[resourceId] = current;

        return true;
    }

    public void Clear()
    {
        _storage.Clear();
    }

    public IReadOnlyDictionary<string, int> ReadOnlySnapshot()
    {
        return _storage;
    }

    public Dictionary<string, int> Snapshot()
    {
        return new Dictionary<string, int>(_storage, StringComparer.OrdinalIgnoreCase);
    }

    public List<ResourceStack> GetStacks()
    {
        var result = new List<ResourceStack>(_storage.Count);

        foreach (var kv in _storage)
        {
            if (kv.Value <= 0)
                continue;

            result.Add(new ResourceStack(kv.Key, kv.Value));
        }

        return result;
    }


    public void LoadFrom(Dictionary<string, int> snapshot)
    {
        _storage.Clear();

        if (snapshot == null)
            return;

        foreach (var kv in snapshot)
        {
            var id = ResourceStack.Normalize(kv.Key);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (kv.Value <= 0)
                continue;

            _storage[id] = kv.Value;
        }
    }
}