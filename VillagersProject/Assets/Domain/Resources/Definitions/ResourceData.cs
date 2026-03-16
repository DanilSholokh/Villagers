using System;
using UnityEngine;

[Serializable]
public class ResourceData
{
    [Header("Core")]
    public string resourceId;
    public string displayName;
    public string categoryId;
    public int baseValue;
    public Sprite icon;

    [Header("Optional")]
    public string description;
    public string[] tags;
    public int sortOrder = 0;
    public bool isHidden = false;

    public string NormalizedResourceId =>
        string.IsNullOrWhiteSpace(resourceId) ? string.Empty : resourceId.Trim().ToLowerInvariant();

    public string NormalizedCategoryId =>
        string.IsNullOrWhiteSpace(categoryId) ? string.Empty : categoryId.Trim().ToLowerInvariant();
}