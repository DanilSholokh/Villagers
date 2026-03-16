using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EconomicRecipe
{
    [Header("Identity")]
    [SerializeField] private string recipeId;
    [SerializeField] private string displayName;

    [Header("Payloads")]
    [SerializeField] private ResourceCost inputCost = new();
    [SerializeField] private ResourceReward outputReward = new();

    [Header("Conditions")]
    [SerializeField] private List<EconomicCondition> conditions = new();

    public string RecipeId => recipeId;
    public string DisplayName => displayName;
    public ResourceCost InputCost => inputCost;
    public ResourceReward OutputReward => outputReward;
    public List<EconomicCondition> Conditions => conditions;

    public bool HasInputs => inputCost != null && !inputCost.IsEmpty;
    public bool HasOutputs => outputReward != null && !outputReward.IsEmpty;
}