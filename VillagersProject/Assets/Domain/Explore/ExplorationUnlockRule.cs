using System;
using UnityEngine;

[Serializable]
public class ExplorationUnlockRule
{
    [Header("Identity")]
    public string ruleId = "discover_region";
    public string displayName = "Discover Region";

    [Header("Cost")]
    public string categoryId = "tradegoods";
    public int requiredValue = 30;

    [Header("Behavior")]
    public bool allowBlockedLocations = false;

    public string NormalizedCategoryId =>
        string.IsNullOrWhiteSpace(categoryId)
            ? string.Empty
            : categoryId.Trim().ToLowerInvariant();
}