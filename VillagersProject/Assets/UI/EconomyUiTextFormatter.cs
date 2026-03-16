using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class EconomyUiTextFormatter
{
    public static string FormatBundle(ResourceBundle bundle)
    {
        if (bundle == null || bundle.IsEmpty)
            return "-";

        var parts = new List<string>();

        var exact = bundle.ExactResources;
        if (exact != null)
        {
            for (int i = 0; i < exact.Count; i++)
            {
                var stack = exact[i];
                if (!stack.IsValid)
                    continue;

                string displayName = GetResourceDisplayName(stack.resourceId);
                parts.Add($"{displayName} x{stack.amount}");
            }
        }

        var categoryValues = bundle.CategoryValues;
        if (categoryValues != null)
        {
            for (int i = 0; i < categoryValues.Count; i++)
            {
                var entry = categoryValues[i];
                if (!entry.IsValid)
                    continue;

                string displayName = GetCategoryDisplayName(entry.categoryId);
                parts.Add($"{displayName} Value x{entry.value}");
            }
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "-";
    }

    public static string FormatBundleLine(string label, ResourceBundle bundle)
    {
        if (bundle == null || bundle.IsEmpty)
            return $"{label}: -";

        return $"{label}: {FormatBundle(bundle)}";
    }

    public static string FormatCategoryValueStacks(IReadOnlyList<CategoryValueStack> values)
    {
        if (values == null || values.Count == 0)
            return "-";

        var parts = new List<string>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            var entry = values[i];
            if (!entry.IsValid)
                continue;

            string displayName = GetCategoryDisplayName(entry.categoryId);
            parts.Add($"{displayName} Value: {entry.value}");
        }

        return parts.Count > 0 ? string.Join("\n", parts) : "-";
    }

    public static string BuildTaskPayloadSummary(TaskInstance task)
    {
        if (task == null)
            return string.Empty;

        var sb = new StringBuilder(256);

        AppendLineIfNotEmpty(sb, "Output", task.GetResolvedWorkOutputBundle());
        AppendLineIfNotEmpty(sb, "Reward", task.GetResolvedTaskRewardBundle());
        AppendLineIfNotEmpty(sb, "Cost", task.GetResolvedTaskCostBundle());

        return sb.ToString().TrimEnd();
    }

    private static void AppendLineIfNotEmpty(StringBuilder sb, string label, ResourceBundle bundle)
    {
        if (bundle == null || bundle.IsEmpty)
            return;

        sb.AppendLine(FormatBundleLine(label, bundle));
    }

    private static string GetResourceDisplayName(string resourceId)
    {
        if (GameInstaller.Resources == null || string.IsNullOrWhiteSpace(resourceId))
            return resourceId ?? string.Empty;

        var resource = GameInstaller.Resources.Get(resourceId);
        if (resource == null)
            return resourceId;

        return string.IsNullOrWhiteSpace(resource.displayName)
            ? resource.resourceId
            : resource.displayName;
    }

    private static string GetCategoryDisplayName(string categoryId)
    {
        if (GameInstaller.ResourceCategories == null || string.IsNullOrWhiteSpace(categoryId))
            return categoryId ?? string.Empty;

        var category = GameInstaller.ResourceCategories.Get(categoryId);
        if (category == null)
            return categoryId;

        return string.IsNullOrWhiteSpace(category.displayName)
            ? category.categoryId
            : category.displayName;
    }
}