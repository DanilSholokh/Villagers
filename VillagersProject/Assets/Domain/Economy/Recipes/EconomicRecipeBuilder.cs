using System.Collections.Generic;
using System.Reflection;

public class EconomicRecipeBuilder
{
    private string _id = string.Empty;
    private string _displayName = string.Empty;
    private ResourceBundle _input;
    private ResourceBundle _output;
    private readonly List<EconomicCondition> _conditions = new();

    public EconomicRecipeBuilder WithId(string id)
    {
        _id = id ?? string.Empty;
        return this;
    }

    public EconomicRecipeBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName ?? string.Empty;
        return this;
    }

    public EconomicRecipeBuilder WithInput(ResourceBundle bundle)
    {
        _input = bundle != null ? bundle.Clone() : null;
        return this;
    }

    public EconomicRecipeBuilder WithOutput(ResourceBundle bundle)
    {
        _output = bundle != null ? bundle.Clone() : null;
        return this;
    }

    public EconomicRecipeBuilder AddCondition(EconomicCondition condition)
    {
        if (condition != null)
            _conditions.Add(condition);

        return this;
    }

    public EconomicRecipe Build()
    {
        var recipe = new EconomicRecipe();

        SetField(recipe, "recipeId", _id);
        SetField(recipe, "displayName", _displayName);
        SetField(recipe, "inputCost", new ResourceCost(_input ?? new ResourceBundle()));
        SetField(recipe, "outputReward", new ResourceReward(_output ?? new ResourceBundle()));
        SetField(recipe, "conditions", new List<EconomicCondition>(_conditions));

        return recipe;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        field?.SetValue(target, value);
    }
}