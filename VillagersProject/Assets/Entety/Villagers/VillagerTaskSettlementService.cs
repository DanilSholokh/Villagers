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

        var reward = task.GetResolvedTaskRewardBundle();
        if (reward == null || reward.IsEmpty)
            return new ResourceBundle();

        return reward.Clone();
    }
}