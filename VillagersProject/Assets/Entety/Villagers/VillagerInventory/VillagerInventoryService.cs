using System;
using System.Collections.Generic;

public class VillagerInventoryService
{
    [Serializable]
    public class ItemStack
    {
        public string itemId;
        public int amount;
    }

    public event Action<string> OnInventoryChanged; // agentId

    private readonly Dictionary<string, List<ItemStack>> _inv = new();

    public IReadOnlyList<ItemStack> Get(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId)) agentId = "unknown";
        if (!_inv.TryGetValue(agentId, out var list))
        {
            list = new List<ItemStack>();
            _inv[agentId] = list;
        }
        return list;
    }

    public void Add(string agentId, string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return;
        if (string.IsNullOrWhiteSpace(agentId)) agentId = "unknown";

        if (!_inv.TryGetValue(agentId, out var list))
        {
            list = new List<ItemStack>();
            _inv[agentId] = list;
        }

        var found = list.Find(x => x.itemId == itemId);
        if (found != null) found.amount += amount;
        else list.Add(new ItemStack { itemId = itemId, amount = amount });

        OnInventoryChanged?.Invoke(agentId);
    }
}