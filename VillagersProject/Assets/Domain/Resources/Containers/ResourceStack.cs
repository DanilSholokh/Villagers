using System;

[Serializable]
public struct ResourceStack
{
    public string resourceId;
    public int amount;

    public ResourceStack(string resourceId, int amount)
    {
        this.resourceId = Normalize(resourceId);
        this.amount = amount;
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(resourceId) && amount > 0;

    public static string Normalize(string resourceId)
    {
        return string.IsNullOrWhiteSpace(resourceId)
            ? string.Empty
            : resourceId.Trim().ToLowerInvariant();
    }
}