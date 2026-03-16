public class EconomicExecutionService
{
    private readonly EconomicValidationService _validation;

    public EconomicExecutionService(EconomicValidationService validation)
    {
        _validation = validation;
    }

    public EconomicResult Execute(EconomicRecipe recipe, EconomicContext context)
    {
        var validation = _validation.Validate(recipe, context);
        if (!validation.success)
            return validation;

        ApplyConsumed(validation, context);
        ApplyProduced(recipe, validation, context);

        validation.success = true;
        validation.message = "Recipe executed successfully.";
        return validation;
    }

    private void ApplyConsumed(EconomicResult result, EconomicContext context)
    {
        var consumed = result.consumed;
        if (consumed == null || consumed.IsEmpty)
            return;

        var exact = consumed.ExactResources;
        for (int i = 0; i < exact.Count; i++)
        {
            var stack = exact[i];
            if (!stack.IsValid)
                continue;

            context.source.TrySpend(stack.resourceId, stack.amount);
        }
    }

    private void ApplyProduced(EconomicRecipe recipe, EconomicResult result, EconomicContext context)
    {
        var reward = recipe.OutputReward != null ? recipe.OutputReward.Bundle : null;
        if (reward == null || reward.IsEmpty)
            return;

        var receiver = context.target ?? context.source;
        var exact = reward.ExactResources;

        if (exact != null)
        {
            for (int i = 0; i < exact.Count; i++)
            {
                var stack = exact[i];
                if (!stack.IsValid)
                    continue;

                receiver.Add(stack.resourceId, stack.amount);
                result.produced.AddExact(stack.resourceId, stack.amount);
            }
        }

        // Category-value rewards are not applied directly in PATCH 6.
        // They are allowed as payload format, but concrete reward materialization
        // will come in later integrations if needed.
    }
}