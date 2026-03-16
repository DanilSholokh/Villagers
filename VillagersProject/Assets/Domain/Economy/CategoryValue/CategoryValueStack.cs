using System;

[Serializable]
public struct CategoryValueStack
{
    public string categoryId;
    public int value;

    public CategoryValueStack(string categoryId, int value)
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