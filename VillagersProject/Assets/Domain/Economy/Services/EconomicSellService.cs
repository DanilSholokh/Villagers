using UnityEngine;

public class EconomicSellService
{
    public int ComputeSellGold(IResourceContainer container)
    {
        if (container == null || GameInstaller.Resources == null || GameInstaller.ResourceCategoryService == null)
            return 0;

        float totalGold = 0f;
        var stacks = container.GetStacks();

        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            if (!stack.IsValid)
                continue;

            if (stack.resourceId == "gold")
                continue;

            var resource = GameInstaller.Resources.Get(stack.resourceId);
            if (resource == null)
                continue;

            int baseValue = Mathf.Max(0, resource.baseValue);
            if (baseValue <= 0)
                continue;

            float tradeMultiplier = Mathf.Max(
                0f,
                GameInstaller.ResourceCategoryService.GetTradeMultiplier(resource.categoryId)
            );

            if (tradeMultiplier <= 0f)
                continue;

            float resourceValue = stack.amount * baseValue;
            totalGold += resourceValue * tradeMultiplier;
        }

        return Mathf.FloorToInt(totalGold);
    }

    public ResourceBundle BuildSellInputBundle(IResourceContainer container)
    {
        var bundle = new ResourceBundle();

        if (container == null)
            return bundle;

        var stacks = container.GetStacks();
        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            if (!stack.IsValid)
                continue;

            if (stack.resourceId == "gold")
                continue;

            bundle.AddExact(stack.resourceId, stack.amount);
        }

        bundle.Normalize();
        return bundle;
    }

    public ResourceBundle BuildSellOutputBundle(IResourceContainer container)
    {
        var bundle = new ResourceBundle();

        int gold = ComputeSellGold(container);
        if (gold > 0)
            bundle.AddExact("gold", gold);

        bundle.Normalize();
        return bundle;
    }

    public EconomicRecipe BuildSellAllRecipe(IResourceContainer container)
    {
        var input = BuildSellInputBundle(container);
        var output = BuildSellOutputBundle(container);

        return new EconomicRecipeBuilder()
            .WithId("sell_all")
            .WithDisplayName("Sell All")
            .WithInput(input)
            .WithOutput(output)
            .Build();
    }
}