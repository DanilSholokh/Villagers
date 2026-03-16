using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VillagerCargo : ISerializationCallbackReceiver, IResourceContainer
{
    [SerializeField] private List<string> keys = new();
    [SerializeField] private List<int> values = new();

    [NonSerialized] private ResourceContainer _container;

    private ResourceContainer Container
    {
        get
        {
            if (_container == null)
                RebuildRuntimeContainerFromSerialized();

            return _container;
        }
    }

    public int Get(string resourceId)
    {
        return GetAmount(resourceId);
    }

    public int GetAmount(string resourceId)
    {
        return Container.GetAmount(resourceId);
    }

    public bool Has(string resourceId, int amount)
    {
        return Container.Has(resourceId, amount);
    }

    public void Add(string resourceId, int amount)
    {
        if (amount <= 0)
            return;

        Container.Add(resourceId, amount);
        SyncSerializedFromContainer();
    }

    public bool TrySpend(string resourceId, int amount)
    {
        var ok = Container.TrySpend(resourceId, amount);
        if (ok)
            SyncSerializedFromContainer();

        return ok;
    }

    public void Clear()
    {
        Container.Clear();
        SyncSerializedFromContainer();
    }

    public Dictionary<string, int> Snapshot()
    {
        return Container.Snapshot();
    }

    public IReadOnlyDictionary<string, int> ReadOnlySnapshot()
    {
        return Container.ReadOnlySnapshot();
    }

    public List<ResourceStack> GetStacks()
    {
        return Container.GetStacks();
    }

    public void LoadFrom(Dictionary<string, int> snapshot)
    {
        Container.LoadFrom(snapshot);
        SyncSerializedFromContainer();
    }

    public void OnBeforeSerialize()
    {
        SyncSerializedFromContainer();
    }

    public void OnAfterDeserialize()
    {
        RebuildRuntimeContainerFromSerialized();
    }

    private void RebuildRuntimeContainerFromSerialized()
    {
        _container = new ResourceContainer();

        int count = Mathf.Min(keys != null ? keys.Count : 0, values != null ? values.Count : 0);
        for (int i = 0; i < count; i++)
        {
            var id = keys[i];
            var amount = values[i];

            if (string.IsNullOrWhiteSpace(id) || amount <= 0)
                continue;

            _container.Add(id, amount);
        }

        SyncSerializedFromContainer();
    }

    private void SyncSerializedFromContainer()
    {
        keys ??= new List<string>();
        values ??= new List<int>();

        keys.Clear();
        values.Clear();

        if (_container == null)
            return;

        var stacks = _container.GetStacks();
        for (int i = 0; i < stacks.Count; i++)
        {
            if (!stacks[i].IsValid)
                continue;

            keys.Add(stacks[i].resourceId);
            values.Add(stacks[i].amount);
        }
    }
}