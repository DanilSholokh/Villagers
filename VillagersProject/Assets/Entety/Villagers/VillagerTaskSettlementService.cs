public class VillagerTaskSettlementService
{
    public void ApplySuccess(TaskInstance task, VillagerCargo cargo, TreasuryService treasury)
    {
        if (task == null || cargo == null || treasury == null)
            return;

        // 1. Commit gathered / produced work output via cargo path.
        //    In current prototype only exact resource work output is supported here.
        var workBundle = task.GetResolvedWorkOutputBundle();
        if (workBundle != null && !workBundle.IsEmpty)
        {
            var exact = workBundle.ExactResources;
            for (int i = 0; i < exact.Count; i++)
            {
                var stack = exact[i];
                if (!stack.IsValid)
                    continue;

                cargo.Add(stack.resourceId, stack.amount);
            }
        }

        // 2. Apply reward bundle directly to treasury through economy endpoint.
        //    Legacy gold wage must not be duplicated when old gold escrow is active.
        var rewardBundle = BuildTreasuryRewardBundle(task);
        if (rewardBundle != null && !rewardBundle.IsEmpty)
        {
            treasury.GrantBundle(rewardBundle, "task_reward_bundle");
        }
    }

    public void ApplyFailure(TaskInstance task, VillagerCargo cargo, TreasuryService treasury)
    {
        if (cargo != null)
            cargo.Clear();
    }

    public void ApplyDeath(TaskInstance task, VillagerCargo cargo, TreasuryService treasury)
    {
        if (cargo != null)
            cargo.Clear();
    }

    public void ApplyLost(TaskInstance task, VillagerCargo cargo, TreasuryService treasury)
    {
        if (cargo != null)
            cargo.Clear();
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