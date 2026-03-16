using System.Collections.Generic;

public class EconomicValidationService
{
    private readonly CategoryValueService _categoryValues;
    private readonly CategoryQueryService _categoryQueries;
    private readonly EconomicSelectionService _selectionService;

    public EconomicValidationService(
        CategoryValueService categoryValues,
        CategoryQueryService categoryQueries,
        EconomicSelectionService selectionService)
    {
        _categoryValues = categoryValues;
        _categoryQueries = categoryQueries;
        _selectionService = selectionService;
    }

    public EconomicResult Validate(EconomicRecipe recipe, EconomicContext context)
    {
        if (recipe == null)
            return EconomicResult.Fail("Recipe is null.");

        if (context == null)
            return EconomicResult.Fail("Economic context is null.");

        if (context.source == null)
            return EconomicResult.Fail("Economic source container is null.");

        var result = EconomicResult.Ok("Recipe validation passed.");

        if (!ValidateConditions(recipe, context, result))
            return result;

        if (!ValidateInputs(recipe, context, result))
            return result;

        return result;
    }

    private bool ValidateConditions(EconomicRecipe recipe, EconomicContext context, EconomicResult result)
    {
        var conditions = recipe.Conditions;
        if (conditions == null || conditions.Count == 0)
            return true;

        for (int i = 0; i < conditions.Count; i++)
        {
            var cond = conditions[i];
            if (cond == null)
                continue;

            switch (cond.Type)
            {
                case EconomicConditionType.HasExactResources:
                    if (!context.source.Has(cond.ResourceId, cond.Amount))
                    {
                        result.success = false;
                        result.message = $"Missing exact resource: {cond.ResourceId} x{cond.Amount}";
                        return false;
                    }
                    break;

                case EconomicConditionType.HasCategoryValue:
                    if (_categoryQueries == null ||
                        !_categoryQueries.HasEnoughCategoryValue(context.source, cond.CategoryId, cond.Amount))
                    {
                        result.success = false;
                        result.message = $"Missing category value: {cond.CategoryId} value {cond.Amount}";
                        return false;
                    }
                    break;
            }
        }

        return true;
    }

    private bool ValidateInputs(EconomicRecipe recipe, EconomicContext context, EconomicResult result)
    {
        var bundle = recipe.InputCost != null ? recipe.InputCost.Bundle : null;
        if (bundle == null || bundle.IsEmpty)
            return true;

        var exact = bundle.ExactResources;
        if (exact != null)
        {
            for (int i = 0; i < exact.Count; i++)
            {
                var stack = exact[i];
                if (!stack.IsValid)
                    continue;

                if (!context.source.Has(stack.resourceId, stack.amount))
                {
                    result.success = false;
                    result.message = $"Missing exact input: {stack.resourceId} x{stack.amount}";
                    return false;
                }

                result.consumed.AddExact(stack.resourceId, stack.amount);
            }
        }

        var categories = bundle.CategoryValues;
        if (categories != null)
        {
            for (int i = 0; i < categories.Count; i++)
            {
                var req = categories[i];
                if (!req.IsValid)
                    continue;

                if (_categoryQueries == null ||
                    !_categoryQueries.HasEnoughCategoryValue(context.source, req.categoryId, req.value))
                {
                    result.success = false;
                    result.message = $"Missing category input: {req.categoryId} value {req.value}";
                    return false;
                }

                var selection = _selectionService != null
                    ? _selectionService.SelectCategorySpend(context.source, req.categoryId, req.value)
                    : new List<ResourceStack>();

                if (selection == null || selection.Count == 0)
                {
                    result.success = false;
                    result.message = $"Unable to build category spend selection: {req.categoryId} value {req.value}";
                    return false;
                }

                for (int s = 0; s < selection.Count; s++)
                {
                    var pick = selection[s];
                    if (!pick.IsValid)
                        continue;

                    result.categorySpendSelection.Add(pick);
                    result.consumed.AddExact(pick.resourceId, pick.amount);
                }
            }
        }

        return true;
    }
}