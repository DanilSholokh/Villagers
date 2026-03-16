using System.Collections.Generic;

public class CategoryQueryService
{
    private readonly CategoryValueService _valueService;

    public CategoryQueryService(CategoryValueService valueService)
    {
        _valueService = valueService;
    }

    public int GetCategoryValue(IResourceContainer container, string categoryId)
    {
        if (_valueService == null)
            return 0;

        return _valueService.GetContainerCategoryValue(container, categoryId);
    }

    public bool HasEnoughCategoryValue(IResourceContainer container, string categoryId, int requiredValue)
    {
        if (_valueService == null)
            return false;

        return _valueService.HasCategoryValue(container, categoryId, requiredValue);
    }

    // Base selection path only.
    // This PATCH does not spend yet; it only determines a deterministic candidate order.
    public List<ResourceStack> BuildSpendSelectionPath(IResourceContainer container, string categoryId)
    {
        var result = new List<ResourceStack>();

        if (_valueService == null || container == null)
            return result;

        categoryId = CategoryValueStack.Normalize(categoryId);
        if (string.IsNullOrWhiteSpace(categoryId))
            return result;

        var stacks = container.GetStacks();

        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            if (!stack.IsValid)
                continue;

            var stackCategoryId = _valueService.GetResourceCategoryId(stack.resourceId);
            if (stackCategoryId != categoryId)
                continue;

            result.Add(stack);
        }

        result.Sort((a, b) =>
        {
            int av = _valueService.GetResourceValue(a.resourceId);
            int bv = _valueService.GetResourceValue(b.resourceId);

            int byUnitValueDesc = bv.CompareTo(av);
            if (byUnitValueDesc != 0)
                return byUnitValueDesc;

            return string.CompareOrdinal(a.resourceId, b.resourceId);
        });

        return result;
    }
}