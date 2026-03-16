using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceRegistry
{
    private readonly Dictionary<string, ResourceData> _byId =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byId.Count;

    public void Clear()
    {
        _byId.Clear();
    }

    public void LoadFromAssets(IEnumerable<ResourceDataSO> assets)
    {
        Clear();

        if (assets == null)
            return;

        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            Register(asset.Data);
        }
    }

    public void Register(ResourceData data)
    {
        if (data == null)
            return;

        var id = NormalizeId(data.resourceId);
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning("[ResourceRegistry] Skip resource with empty resourceId.");
            return;
        }

        data.resourceId = id;

        if (!string.IsNullOrWhiteSpace(data.categoryId))
            data.categoryId = NormalizeId(data.categoryId);

        _byId[id] = data;
    }

    public bool Has(string resourceId)
    {
        return _byId.ContainsKey(NormalizeId(resourceId));
    }

    public bool TryGet(string resourceId, out ResourceData data)
    {
        return _byId.TryGetValue(NormalizeId(resourceId), out data);
    }

    public ResourceData Get(string resourceId)
    {
        return _byId.TryGetValue(NormalizeId(resourceId), out var data) ? data : null;
    }

    public IReadOnlyCollection<ResourceData> GetAll()
    {
        return _byId.Values;
    }

    private static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? string.Empty
            : id.Trim().ToLowerInvariant();
    }
}