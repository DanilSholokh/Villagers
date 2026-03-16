using System;
using UnityEngine;

public enum EconomicConditionType
{
    None = 0,
    HasExactResources = 1,
    HasCategoryValue = 2,
}

[Serializable]
public class EconomicCondition
{
    [SerializeField] private EconomicConditionType type = EconomicConditionType.None;
    [SerializeField] private string resourceId;
    [SerializeField] private string categoryId;
    [SerializeField] private int amount;

    public EconomicConditionType Type => type;
    public string ResourceId => resourceId;
    public string CategoryId => categoryId;
    public int Amount => amount;

    public static EconomicCondition RequireExact(string resourceId, int amount)
    {
        return new EconomicCondition
        {
            type = EconomicConditionType.HasExactResources,
            resourceId = ResourceStack.Normalize(resourceId),
            amount = Mathf.Max(0, amount)
        };
    }

    public static EconomicCondition RequireCategoryValue(string categoryId, int amount)
    {
        return new EconomicCondition
        {
            type = EconomicConditionType.HasCategoryValue,
            categoryId = CategoryValueRequirement.Normalize(categoryId),
            amount = Mathf.Max(0, amount)
        };
    }
}