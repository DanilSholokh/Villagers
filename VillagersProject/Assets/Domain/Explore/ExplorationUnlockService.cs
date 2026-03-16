using UnityEngine;

public class ExplorationUnlockService
{
    private readonly ExplorationUnlockRule _rule;

    public ExplorationUnlockService(ExplorationUnlockRule rule)
    {
        _rule = rule ?? new ExplorationUnlockRule();
    }

    public string RuleId =>
        string.IsNullOrWhiteSpace(_rule.ruleId)
            ? "discover_region"
            : _rule.ruleId;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(_rule.displayName)
            ? "Discover Region"
            : _rule.displayName;

    public string CategoryId =>
        string.IsNullOrWhiteSpace(_rule.NormalizedCategoryId)
            ? "tradegoods"
            : _rule.NormalizedCategoryId;

    public int RequiredValue => Mathf.Max(0, _rule.requiredValue);

    public bool AllowBlockedLocations => _rule.allowBlockedLocations;

    public string GetCostText()
    {
        return $"{RequiredValue} {GetCategoryDisplayName()} Value";
    }

    public ResourceBundle BuildCostBundle()
    {
        var bundle = new ResourceBundle();

        if (RequiredValue > 0)
        {
            bundle.AddCategoryValue(CategoryId, RequiredValue);
            bundle.Normalize();
        }

        return bundle;
    }

    public bool CanAfford(TreasuryService treasury, out string reason)
    {
        reason = string.Empty;

        if (treasury == null)
        {
            reason = "Treasury is null";
            return false;
        }

        var bundle = BuildCostBundle();
        if (bundle == null || bundle.IsEmpty)
            return true;

        if (!treasury.CanAfford(bundle))
        {
            reason = "Not enough category value";
            return false;
        }

        return true;
    }

    public int GetCurrentCategoryValue(TreasuryService treasury)
    {
        if (treasury == null || GameInstaller.CategoryValues == null)
            return 0;

        return GameInstaller.CategoryValues.GetContainerCategoryValue(treasury, CategoryId);
    }

    private string GetCategoryDisplayName()
    {
        if (GameInstaller.ResourceCategories == null || string.IsNullOrWhiteSpace(CategoryId))
            return CategoryId;

        var cat = GameInstaller.ResourceCategories.Get(CategoryId);
        if (cat == null)
            return CategoryId;

        return string.IsNullOrWhiteSpace(cat.displayName)
            ? CategoryId
            : cat.displayName;
    }
}