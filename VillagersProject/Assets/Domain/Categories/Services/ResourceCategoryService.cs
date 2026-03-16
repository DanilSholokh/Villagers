using UnityEngine;

public class ResourceCategoryService
{
    private readonly ResourceCategoryRegistry _registry;

    public ResourceCategoryService(ResourceCategoryRegistry registry)
    {
        _registry = registry;
    }

    public bool HasCategory(string categoryId)
    {
        return _registry != null && _registry.Has(categoryId);
    }

    public ResourceCategoryData GetCategory(string categoryId)
    {
        return _registry != null ? _registry.Get(categoryId) : null;
    }

    public float GetTradeMultiplier(string categoryId)
    {
        var category = GetCategory(categoryId);
        return category != null ? Mathf.Max(0f, category.tradeMultiplier) : 0f;
    }

    public float GetTradeMultiplierByResourceId(string resourceId)
    {
        if (GameInstaller.Resources == null || string.IsNullOrWhiteSpace(resourceId))
            return 0f;

        var resource = GameInstaller.Resources.Get(resourceId);
        if (resource == null)
            return 0f;

        return GetTradeMultiplier(resource.categoryId);
    }

    public bool ResourceHasValidCategory(string resourceId)
    {
        if (GameInstaller.Resources == null || string.IsNullOrWhiteSpace(resourceId))
            return false;

        var resource = GameInstaller.Resources.Get(resourceId);
        return resource != null && HasCategory(resource.categoryId);
    }
}