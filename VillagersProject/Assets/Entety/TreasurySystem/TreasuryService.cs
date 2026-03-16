using System;
using System.Collections.Generic;

public class TreasuryService : IResourceContainer
{
    public event Action<string, int> OnChanged;

    private readonly ResourceContainer _container = new();
    private readonly ResourceService _resourceService = new();

    private int lockedGold = 0;

    public int LockedGold => lockedGold;

    public int GetAmount(string resourceId)
    {
        return _resourceService.GetAmount(_container, resourceId);
    }

    public bool Has(string resourceId, int amount)
    {
        return _container.Has(resourceId, amount);
    }

    public void Add(string resourceId, int amount)
    {
        if (amount <= 0)
            return;

        _resourceService.Add(_container, resourceId, amount);
        NotifyChanged(resourceId);
    }

    public bool TrySpend(string resourceId, int amount)
    {
        if (amount <= 0)
            return true;

        bool ok = _resourceService.TrySpend(_container, resourceId, amount);
        if (ok)
            NotifyChanged(resourceId);

        return ok;
    }

    public void Clear()
    {
        ClearAll();
    }

    public Dictionary<string, int> Snapshot()
    {
        return _resourceService.Snapshot(_container);
    }

    public IReadOnlyDictionary<string, int> ReadOnlySnapshot()
    {
        return _container.ReadOnlySnapshot();
    }

    public List<ResourceStack> GetStacks()
    {
        return _container.GetStacks();
    }

    // -------------------------
    // PATCH 7: Treasury Economy Endpoint
    // -------------------------

    public EconomicResult ValidateRecipe(
        EconomicRecipe recipe,
        string actorId = null,
        string reason = null)
    {
        if (GameInstaller.EconomicValidation == null)
            return EconomicResult.Fail("EconomicValidationService is not ready.");

        var ctx = EconomicTreasuryContextFactory.Create(this, actorId, reason);
        return GameInstaller.EconomicValidation.Validate(recipe, ctx);
    }

    public EconomicResult SimulateRecipe(
        EconomicRecipe recipe,
        string actorId = null,
        string reason = null)
    {
        if (GameInstaller.EconomicSimulation == null)
            return EconomicResult.Fail("EconomicSimulationService is not ready.");

        var ctx = EconomicTreasuryContextFactory.Create(this, actorId, reason);
        return GameInstaller.EconomicSimulation.Simulate(recipe, ctx);
    }

    public EconomicResult ExecuteRecipe(
        EconomicRecipe recipe,
        string actorId = null,
        string reason = null)
    {
        if (GameInstaller.EconomicExecution == null)
            return EconomicResult.Fail("EconomicExecutionService is not ready.");

        var before = Snapshot();

        var ctx = EconomicTreasuryContextFactory.Create(this, actorId, reason);
        var result = GameInstaller.EconomicExecution.Execute(recipe, ctx);

        if (result.success)
            NotifyDiff(before);

        return result;
    }

    public bool CanAfford(ResourceBundle bundle)
    {
        if (bundle == null || bundle.IsEmpty)
            return true;

        var recipe = new EconomicRecipeBuilder()
            .WithId("treasury_can_afford")
            .WithDisplayName("Treasury CanAfford Check")
            .WithInput(bundle)
            .Build();

        var result = ValidateRecipe(recipe, reason: "treasury_can_afford");
        return result.success;
    }

    public EconomicResult SpendBundle(ResourceBundle bundle, string reason = null)
    {
        if (bundle == null || bundle.IsEmpty)
            return EconomicResult.Ok("Bundle is empty.");

        var recipe = new EconomicRecipeBuilder()
            .WithId("treasury_spend_bundle")
            .WithDisplayName("Treasury Spend Bundle")
            .WithInput(bundle)
            .Build();

        return ExecuteRecipe(recipe, reason: reason ?? "treasury_spend_bundle");
    }

    public EconomicResult GrantBundle(ResourceBundle bundle, string reason = null)
    {
        if (bundle == null || bundle.IsEmpty)
            return EconomicResult.Ok("Bundle is empty.");

        var recipe = new EconomicRecipeBuilder()
            .WithId("treasury_grant_bundle")
            .WithDisplayName("Treasury Grant Bundle")
            .WithOutput(bundle)
            .Build();

        return ExecuteRecipe(recipe, reason: reason ?? "treasury_grant_bundle");
    }

    public int GetCategoryValue(string categoryId)
    {
        if (GameInstaller.CategoryQueries == null)
            return 0;

        return GameInstaller.CategoryQueries.GetCategoryValue(this, categoryId);
    }

    public bool HasCategoryValue(string categoryId, int requiredValue)
    {
        if (GameInstaller.CategoryQueries == null)
            return false;

        return GameInstaller.CategoryQueries.HasEnoughCategoryValue(this, categoryId, requiredValue);
    }

    // -------------------------
    // Back-compat API
    // -------------------------

    public int SellAllToGold()
    {
        if (GameInstaller.EconomicSell == null)
            return 0;

        int gainedGold = GameInstaller.EconomicSell.ComputeSellGold(this);
        if (gainedGold <= 0)
            return 0;

        var recipe = GameInstaller.EconomicSell.BuildSellAllRecipe(this);
        var result = ExecuteRecipe(recipe, reason: "sell_all");

        return result.success ? gainedGold : 0;
    }

    // Back-compat overload.
    // UI більше не повинен передавати локальний price dictionary.
    public int SellAllToGold(Dictionary<string, int> _legacyPriceByResId)
    {
        return SellAllToGold();
    }

    public void ClearAll()
    {
        var snapshot = _container.Snapshot();
        _container.Clear();

        foreach (var kv in snapshot)
            NotifyChanged(kv.Key);
    }

    public int GetAvailableGold()
    {
        return GetAmount("gold");
    }

    public int GetLockedGold()
    {
        return lockedGold;
    }

    public string GetGoldDisplayText()
    {
        int avail = GetAvailableGold();
        int locked = GetLockedGold();

        return locked > 0
            ? $"Gold {avail} (🔒{locked})"
            : $"Gold {avail}";
    }

    public string GetGoldUi()
    {
        return GetGoldDisplayText();
    }

    public bool TryHoldGold(int amount)
    {
        if (amount <= 0)
            return true;

        int avail = GetAmount("gold");
        if (avail < amount)
            return false;

        _container.TrySpend("gold", amount);
        lockedGold += amount;

        NotifyChanged("gold");
        return true;
    }

    public void RefundGold(int amount)
    {
        if (amount <= 0)
            return;

        lockedGold -= amount;
        if (lockedGold < 0)
            lockedGold = 0;

        _container.Add("gold", amount);
        NotifyChanged("gold");
    }

    public void ConsumeLockedGold(int amount)
    {
        if (amount <= 0)
            return;

        lockedGold -= amount;
        if (lockedGold < 0)
            lockedGold = 0;

        NotifyChanged("gold");
    }

    public void InitializeGold(int amount)
    {
        Add("gold", amount);
    }

    private void NotifyChanged(string resourceId)
    {
        OnChanged?.Invoke(resourceId, GetAmount(resourceId));
    }

    private void NotifyDiff(Dictionary<string, int> before)
    {
        var after = Snapshot();

        var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in before)
            touched.Add(kv.Key);

        foreach (var kv in after)
            touched.Add(kv.Key);

        foreach (var id in touched)
        {
            int beforeAmount = before.TryGetValue(id, out var b) ? b : 0;
            int afterAmount = after.TryGetValue(id, out var a) ? a : 0;

            if (beforeAmount != afterAmount)
                NotifyChanged(id);
        }
    }
}