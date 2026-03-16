using System.Collections.Generic;

public class CategoryValueService
{
    public int GetResourceValue(string resourceId)
    {
        if (GameInstaller.Resources == null || string.IsNullOrWhiteSpace(resourceId))
            return 0;

        var resource = GameInstaller.Resources.Get(resourceId);
        if (resource == null)
            return 0;

        return resource.baseValue < 0 ? 0 : resource.baseValue;
    }

    public string GetResourceCategoryId(string resourceId)
    {
        if (GameInstaller.Resources == null || string.IsNullOrWhiteSpace(resourceId))
            return string.Empty;

        var resource = GameInstaller.Resources.Get(resourceId);
        if (resource == null || string.IsNullOrWhiteSpace(resource.categoryId))
            return string.Empty;

        return CategoryValueStack.Normalize(resource.categoryId);
    }

    public int GetStackCategoryValue(ResourceStack stack)
    {
        if (!stack.IsValid)
            return 0;

        int unitValue = GetResourceValue(stack.resourceId);
        if (unitValue <= 0)
            return 0;

        return stack.amount * unitValue;
    }

    public int GetContainerCategoryValue(IResourceContainer container, string categoryId)
    {
        if (container == null)
            return 0;

        categoryId = CategoryValueStack.Normalize(categoryId);
        if (string.IsNullOrWhiteSpace(categoryId))
            return 0;

        int total = 0;
        var stacks = container.GetStacks();

        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            if (!stack.IsValid)
                continue;

            var resourceCategoryId = GetResourceCategoryId(stack.resourceId);
            if (resourceCategoryId != categoryId)
                continue;

            total += GetStackCategoryValue(stack);
        }

        return total;
    }

    public List<CategoryValueStack> BuildCategoryValueStacks(IResourceContainer container)
    {
        var result = new Dictionary<string, int>();

        if (container == null)
            return new List<CategoryValueStack>();

        var stacks = container.GetStacks();
        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            if (!stack.IsValid)
                continue;

            var categoryId = GetResourceCategoryId(stack.resourceId);
            if (string.IsNullOrWhiteSpace(categoryId))
                continue;

            int stackValue = GetStackCategoryValue(stack);
            if (stackValue <= 0)
                continue;

            if (!result.ContainsKey(categoryId))
                result[categoryId] = 0;

            result[categoryId] += stackValue;
        }

        var list = new List<CategoryValueStack>(result.Count);
        foreach (var kv in result)
            list.Add(new CategoryValueStack(kv.Key, kv.Value));

        return list;
    }

    public bool HasCategoryValue(IResourceContainer container, string categoryId, int requiredValue)
    {
        if (requiredValue <= 0)
            return true;

        return GetContainerCategoryValue(container, categoryId) >= requiredValue;
    }
}