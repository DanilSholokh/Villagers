using System;
using UnityEngine;

[Serializable]
public class ResourceCategoryData
{
    [Header("Core")]
    public string categoryId;
    public string displayName;
    [Min(0f)] public float tradeMultiplier = 1f;

    public string NormalizedCategoryId =>
        string.IsNullOrWhiteSpace(categoryId) ? string.Empty : categoryId.Trim().ToLowerInvariant();
}