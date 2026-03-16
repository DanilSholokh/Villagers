public class EconomicSimulationService
{
    private readonly EconomicValidationService _validation;

    public EconomicSimulationService(EconomicValidationService validation)
    {
        _validation = validation;
    }

    public EconomicResult Simulate(EconomicRecipe recipe, EconomicContext context)
    {
        var validation = _validation.Validate(recipe, context);
        if (!validation.success)
            return validation;

        var reward = recipe.OutputReward != null ? recipe.OutputReward.Bundle : null;
        if (reward != null && !reward.IsEmpty)
        {
            var exact = reward.ExactResources;
            if (exact != null)
            {
                for (int i = 0; i < exact.Count; i++)
                {
                    var stack = exact[i];
                    if (!stack.IsValid)
                        continue;

                    validation.produced.AddExact(stack.resourceId, stack.amount);
                }
            }
        }

        validation.success = true;
        validation.message = "Recipe simulation successful.";
        return validation;
    }
}