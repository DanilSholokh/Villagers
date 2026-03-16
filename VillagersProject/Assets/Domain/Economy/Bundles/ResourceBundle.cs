using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ResourceBundle
{
    [SerializeField] private List<ResourceStack> exactResources = new();
    [SerializeField] private List<CategoryValueRequirement> categoryValues = new();

    public List<ResourceStack> ExactResources => exactResources;
    public List<CategoryValueRequirement> CategoryValues => categoryValues;

    public bool IsEmpty
    {
        get
        {
            return (exactResources == null || exactResources.Count == 0)
                && (categoryValues == null || categoryValues.Count == 0);
        }
    }

    public void Clear()
    {
        exactResources.Clear();
        categoryValues.Clear();
    }

    public void AddExact(string resourceId, int amount)
    {
        var stack = new ResourceStack(resourceId, amount);
        if (!stack.IsValid)
            return;

        for (int i = 0; i < exactResources.Count; i++)
        {
            if (string.Equals(exactResources[i].resourceId, stack.resourceId, StringComparison.OrdinalIgnoreCase))
            {
                var merged = exactResources[i];
                merged.amount += stack.amount;
                exactResources[i] = merged;
                return;
            }
        }

        exactResources.Add(stack);
    }

    public void AddCategoryValue(string categoryId, int value)
    {
        var entry = new CategoryValueRequirement(categoryId, value);
        if (!entry.IsValid)
            return;

        for (int i = 0; i < categoryValues.Count; i++)
        {
            if (string.Equals(categoryValues[i].categoryId, entry.categoryId, StringComparison.OrdinalIgnoreCase))
            {
                var merged = categoryValues[i];
                merged.value += entry.value;
                categoryValues[i] = merged;
                return;
            }
        }

        categoryValues.Add(entry);
    }

    public int GetExactAmount(string resourceId)
    {
        resourceId = ResourceStack.Normalize(resourceId);
        if (string.IsNullOrWhiteSpace(resourceId))
            return 0;

        int total = 0;
        for (int i = 0; i < exactResources.Count; i++)
        {
            if (exactResources[i].resourceId == resourceId)
                total += exactResources[i].amount;
        }

        return total;
    }

    public int GetCategoryValue(string categoryId)
    {
        categoryId = CategoryValueRequirement.Normalize(categoryId);
        if (string.IsNullOrWhiteSpace(categoryId))
            return 0;

        int total = 0;
        for (int i = 0; i < categoryValues.Count; i++)
        {
            if (categoryValues[i].categoryId == categoryId)
                total += categoryValues[i].value;
        }

        return total;
    }

    public ResourceBundle Clone()
    {
        var clone = new ResourceBundle();

        for (int i = 0; i < exactResources.Count; i++)
        {
            var s = exactResources[i];
            if (s.IsValid)
                clone.exactResources.Add(new ResourceStack(s.resourceId, s.amount));
        }

        for (int i = 0; i < categoryValues.Count; i++)
        {
            var c = categoryValues[i];
            if (c.IsValid)
                clone.categoryValues.Add(new CategoryValueRequirement(c.categoryId, c.value));
        }

        return clone;
    }

    public void Normalize()
    {
        var normalizedExact = new List<ResourceStack>();
        var normalizedCategories = new List<CategoryValueRequirement>();

        foreach (var stack in exactResources)
        {
            if (!stack.IsValid)
                continue;

            bool merged = false;
            for (int i = 0; i < normalizedExact.Count; i++)
            {
                if (normalizedExact[i].resourceId == stack.resourceId)
                {
                    var s = normalizedExact[i];
                    s.amount += stack.amount;
                    normalizedExact[i] = s;
                    merged = true;
                    break;
                }
            }

            if (!merged)
                normalizedExact.Add(new ResourceStack(stack.resourceId, stack.amount));
        }

        foreach (var entry in categoryValues)
        {
            if (!entry.IsValid)
                continue;

            bool merged = false;
            for (int i = 0; i < normalizedCategories.Count; i++)
            {
                if (normalizedCategories[i].categoryId == entry.categoryId)
                {
                    var c = normalizedCategories[i];
                    c.value += entry.value;
                    normalizedCategories[i] = c;
                    merged = true;
                    break;
                }
            }

            if (!merged)
                normalizedCategories.Add(new CategoryValueRequirement(entry.categoryId, entry.value));
        }

        exactResources = normalizedExact;
        categoryValues = normalizedCategories;
    }
}