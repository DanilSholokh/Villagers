using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceCategoryRegistry
{
    private readonly Dictionary<string, ResourceCategoryData> _byId =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byId.Count;

    public void Clear()
    {
        _byId.Clear();
    }

    public void LoadFromAssets(IEnumerable<ResourceCategoryDataSO> assets)
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

    public void Register(ResourceCategoryData data)
    {
        if (data == null)
            return;

        var id = NormalizeId(data.categoryId);
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning("[ResourceCategoryRegistry] Skip category with empty categoryId.");
            return;
        }

        data.categoryId = id;
        _byId[id] = data;
    }

    public bool Has(string categoryId)
    {
        return _byId.ContainsKey(NormalizeId(categoryId));
    }

    public bool TryGet(string categoryId, out ResourceCategoryData data)
    {
        return _byId.TryGetValue(NormalizeId(categoryId), out data);
    }

    public ResourceCategoryData Get(string categoryId)
    {
        return _byId.TryGetValue(NormalizeId(categoryId), out var data) ? data : null;
    }

    public IReadOnlyCollection<ResourceCategoryData> GetAll()
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