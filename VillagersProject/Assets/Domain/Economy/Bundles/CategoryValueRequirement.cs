using System;

[Serializable]
public struct CategoryValueRequirement
{
    public string categoryId;
    public int value;

    public CategoryValueRequirement(string categoryId, int value)
    {
        this.categoryId = Normalize(categoryId);
        this.value = value;
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(categoryId) && value > 0;

    public static string Normalize(string categoryId)
    {
        return string.IsNullOrWhiteSpace(categoryId)
            ? string.Empty
            : categoryId.Trim().ToLowerInvariant();
    }
}