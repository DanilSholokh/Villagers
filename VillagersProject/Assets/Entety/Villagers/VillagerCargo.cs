using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VillagerCargo
{
    [SerializeField] private List<string> keys = new();
    [SerializeField] private List<int> values = new();

    // runtime map
    private Dictionary<string, int> _map;

    private Dictionary<string, int> Map
    {
        get
        {
            if (_map == null)
            {
                _map = new Dictionary<string, int>();
                for (int i = 0; i < Mathf.Min(keys.Count, values.Count); i++)
                    _map[keys[i]] = values[i];
            }
            return _map;
        }
    }

    public void Add(string resId, int amount)
    {
        if (string.IsNullOrEmpty(resId) || amount <= 0) return;
        Map.TryGetValue(resId, out var cur);
        Map[resId] = cur + amount;
        SyncLists();
    }

    public int Get(string resId)
    {
        if (string.IsNullOrEmpty(resId)) return 0;
        return Map.TryGetValue(resId, out var cur) ? cur : 0;
    }

    public Dictionary<string, int> Snapshot()
    {
        return new Dictionary<string, int>(Map);
    }

    public void Clear()
    {
        Map.Clear();
        SyncLists();
    }

    private void SyncLists()
    {
        keys.Clear();
        values.Clear();
        foreach (var kv in Map)
        {
            keys.Add(kv.Key);
            values.Add(kv.Value);
        }
    }
}
