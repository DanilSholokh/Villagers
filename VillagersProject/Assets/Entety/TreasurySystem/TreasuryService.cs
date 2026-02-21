using System;
using System.Collections.Generic;
using UnityEngine;

public class TreasuryService
{
    // Єдине джерело істини
    private readonly Dictionary<string, int> storage = new();

    public event Action<string, int> OnChanged;

    public int GetAmount(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) return 0;
        resourceId = resourceId.ToLowerInvariant();

        return storage.TryGetValue(resourceId, out var v) ? v : 0;
    }

    // Back-compat: щоб існуючий UI/код, який викликає Get("wood"), працював
    public int Get(string resId) => GetAmount(resId);

    public void Add(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
            return;

        resourceId = resourceId.ToLowerInvariant();

        storage.TryGetValue(resourceId, out var cur);
        storage[resourceId] = cur + amount;

        OnChanged?.Invoke(resourceId, storage[resourceId]);
        Debug.Log($"[Treasury] +{amount} {resourceId} (total={storage[resourceId]})");
    }

    public bool TrySpend(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
            return false;

        resourceId = resourceId.ToLowerInvariant();
        int cur = GetAmount(resourceId);

        if (cur < amount) return false;

        storage[resourceId] = cur - amount;
        OnChanged?.Invoke(resourceId, storage[resourceId]);
        return true;
    }

    public void SetAmount(string resourceId, int newAmount)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) return;

        resourceId = resourceId.ToLowerInvariant();
        newAmount = Mathf.Max(0, newAmount);

        storage[resourceId] = newAmount;
        OnChanged?.Invoke(resourceId, newAmount);
    }




    public int SellAllToGold(Dictionary<string, int> priceByResId)
    {
        if (priceByResId == null) return 0;

        int gainedGold = 0;

        foreach (var kv in priceByResId)
        {
            var resId = kv.Key?.ToLowerInvariant();
            int price = kv.Value;

            if (string.IsNullOrWhiteSpace(resId)) continue;
            if (price <= 0) continue;

            int amount = GetAmount(resId);
            if (amount <= 0) continue;

            gainedGold += amount * price;

            // обнуляємо ресурс
            storage[resId] = 0;
            OnChanged?.Invoke(resId, 0);
        }

        if (gainedGold > 0)
        {
            Add("gold", gainedGold);
            Debug.Log($"[Treasury] SellAll -> +{gainedGold} gold");
        }

        return gainedGold;
    }





}

