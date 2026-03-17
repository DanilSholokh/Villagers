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

    public EconomicRecipe BuildUnlockRecipe()
    {
        var input = BuildCostBundle();

        return new EconomicRecipeBuilder()
            .WithId(RuleId)
            .WithDisplayName(DisplayName)
            .WithInput(input)
            .AddCondition(EconomicCondition.RequireCategoryValue(CategoryId, RequiredValue))
            .Build();
    }

    public bool CanAfford(TreasuryService treasury, out string reason)
    {
        reason = string.Empty;

        if (treasury == null)
        {
            reason = "Treasury is null";
            return false;
        }

        if (GameInstaller.EconomicSimulation == null)
        {
            reason = "EconomicSimulationService is null";
            return false;
        }

        var recipe = BuildUnlockRecipe();
        var context = EconomicTreasuryContextFactory.Create(
            treasury,
            actorId: "explore_unlock_preview",
            reason: RuleId
        );

        var result = GameInstaller.EconomicSimulation.Simulate(recipe, context);
        if (!result.success)
        {
            reason = string.IsNullOrWhiteSpace(result.message)
                ? "Explore unlock is not affordable"
                : result.message;
            return false;
        }

        return true;
    }

    public bool TryExecuteUnlock(
        TreasuryService treasury,
        string actorId,
        string reason,
        out EconomicResult result)
    {
        result = EconomicResult.Fail("Explore unlock not executed.");

        if (treasury == null)
        {
            result = EconomicResult.Fail("Treasury is null.");
            return false;
        }

        if (GameInstaller.EconomicExecution == null)
        {
            result = EconomicResult.Fail("EconomicExecutionService is null.");
            return false;
        }

        var recipe = BuildUnlockRecipe();
        var context = EconomicTreasuryContextFactory.Create(
            treasury,
            actorId: actorId,
            reason: string.IsNullOrWhiteSpace(reason) ? RuleId : reason
        );

        result = GameInstaller.EconomicExecution.Execute(recipe, context);
        return result != null && result.success;
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