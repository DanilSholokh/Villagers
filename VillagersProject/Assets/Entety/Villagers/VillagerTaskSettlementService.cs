public class VillagerTaskSettlementService
{
    public void ApplySuccess(TaskInstance task, VillagerCargo cargo, TreasuryService treasury)
    {
        if (task == null || cargo == null || treasury == null)
            return;

        ApplyWorkOutputToCargo(task, cargo);
        ApplyTreasuryReward(task, treasury);
    }

    public void ApplyFailure(TaskInstance task, VillagerCargo cargo, TreasuryService treasury)
    {
        ApplyNonSuccessOutcome(cargo);
    }

    public void ApplyDeath(TaskInstance task, VillagerCargo cargo, TreasuryService treasury)
    {
        ApplyNonSuccessOutcome(cargo);
    }

    public void ApplyLost(TaskInstance task, VillagerCargo cargo, TreasuryService treasury)
    {
        ApplyNonSuccessOutcome(cargo);
    }

    private void ApplyNonSuccessOutcome(VillagerCargo cargo)
    {
        if (cargo != null)
            cargo.Clear();
    }

    private void ApplyWorkOutputToCargo(TaskInstance task, VillagerCargo cargo)
    {
        if (task == null || cargo == null)
            return;

        var workBundle = task.GetResolvedWorkOutputBundle();
        if (workBundle == null || workBundle.IsEmpty)
            return;

        var exact = workBundle.ExactResources;
        if (exact == null)
            return;

        for (int i = 0; i < exact.Count; i++)
        {
            var stack = exact[i];
            if (!stack.IsValid)
                continue;

            cargo.Add(stack.resourceId, stack.amount);
        }
    }

    private void ApplyTreasuryReward(TaskInstance task, TreasuryService treasury)
    {
        if (task == null || treasury == null)
            return;

        var rewardBundle = BuildTreasuryRewardBundle(task);
        if (rewardBundle == null || rewardBundle.IsEmpty)
            return;

        treasury.GrantBundle(rewardBundle, "task_reward_bundle");
    }

    private bool UsesLegacyGoldEscrow(TaskInstance task)
    {
        if (task == null)
            return false;

        var upfrontCostBundle = task.GetResolvedTaskCostBundle();
        return upfrontCostBundle == null || upfrontCostBundle.IsEmpty;
    }

    private ResourceBundle BuildTreasuryRewardBundle(TaskInstance task)
    {
        if (task == null)
            return new ResourceBundle();

        var source = task.GetResolvedTaskRewardBundle();
        if (source == null || source.IsEmpty)
            return new ResourceBundle();

        var result = new ResourceBundle();

        int legacyEscrowGoldToSkip = UsesLegacyGoldEscrow(task) && task.wageGold > 0
            ? task.wageGold
            : 0;

        var exact = source.ExactResources;
        if (exact != null)
        {
            for (int i = 0; i < exact.Count; i++)
            {
                var stack = exact[i];
                if (!stack.IsValid)
                    continue;

                if (stack.resourceId == "gold" && legacyEscrowGoldToSkip > 0)
                {
                    int remainingGold = stack.amount - legacyEscrowGoldToSkip;
                    if (remainingGold > 0)
                        result.AddExact("gold", remainingGold);

                    legacyEscrowGoldToSkip = 0;
                    continue;
                }

                result.AddExact(stack.resourceId, stack.amount);
            }
        }

        var categoryValues = source.CategoryValues;
        if (categoryValues != null)
        {
            for (int i = 0; i < categoryValues.Count; i++)
            {
                var entry = categoryValues[i];
                if (!entry.IsValid)
                    continue;

                result.AddCategoryValue(entry.categoryId, entry.value);
            }
        }

        result.Normalize();
        return result;
    }
}