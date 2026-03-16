using System.Collections.Generic;

public class EconomicSelectionService
{
    private readonly CategoryValueService _categoryValues;
    private readonly CategoryQueryService _categoryQueries;

    public EconomicSelectionService(
        CategoryValueService categoryValues,
        CategoryQueryService categoryQueries)
    {
        _categoryValues = categoryValues;
        _categoryQueries = categoryQueries;
    }

    public List<ResourceStack> SelectCategorySpend(
        IResourceContainer container,
        string categoryId,
        int requiredValue)
    {
        var result = new List<ResourceStack>();

        if (container == null || _categoryValues == null || _categoryQueries == null)
            return result;

        if (requiredValue <= 0)
            return result;

        var ordered = _categoryQueries.BuildSpendSelectionPath(container, categoryId);
        int remaining = requiredValue;

        for (int i = 0; i < ordered.Count; i++)
        {
            var stack = ordered[i];
            if (!stack.IsValid)
                continue;

            int unitValue = _categoryValues.GetResourceValue(stack.resourceId);
            if (unitValue <= 0)
                continue;

            int maxStackValue = stack.amount * unitValue;
            if (maxStackValue <= 0)
                continue;

            int amountToTake = remaining / unitValue;
            if (remaining % unitValue != 0)
                amountToTake++;

            if (amountToTake <= 0)
                continue;

            if (amountToTake > stack.amount)
                amountToTake = stack.amount;

            result.Add(new ResourceStack(stack.resourceId, amountToTake));
            remaining -= amountToTake * unitValue;

            if (remaining <= 0)
                break;
        }

        if (remaining > 0)
            result.Clear();

        return result;
    }
}