public class TaskEscrowService
{
    public bool TryReserve(
        TaskInstance task,
        string agentId,
        TreasuryService treasury,
        out TaskEscrowReservation reservation,
        out string errorMessage)
    {
        reservation = new TaskEscrowReservation
        {
            taskId = task != null ? task.taskId ?? string.Empty : string.Empty,
            agentId = agentId ?? string.Empty
        };

        errorMessage = string.Empty;

        if (task == null)
        {
            errorMessage = "Task is null.";
            return false;
        }

        if (treasury == null)
        {
            return true;
        }

        var upfrontCostBundle = task.GetResolvedTaskCostBundle();
        if (upfrontCostBundle != null && !upfrontCostBundle.IsEmpty)
        {
            if (!treasury.CanAfford(upfrontCostBundle))
            {
                errorMessage = $"Not enough upfront bundle cost for task={task.taskId}";
                return false;
            }

            var spendResult = treasury.SpendBundle(upfrontCostBundle, "task_upfront_cost");
            if (!spendResult.success)
            {
                errorMessage = $"Upfront bundle spend failed for task={task.taskId} reason={spendResult.message}";
                return false;
            }

            reservation.usesBundleCost = true;
            reservation.spentBundle = spendResult.consumed != null && !spendResult.consumed.IsEmpty
                ? spendResult.consumed.Clone()
                : upfrontCostBundle.Clone();

            return true;
        }

        if (task.wageGold > 0)
        {
            if (!treasury.TryHoldGold(task.wageGold))
            {
                errorMessage = $"Not enough gold for wage={task.wageGold}";
                return false;
            }

            reservation.usesLegacyGold = true;
            reservation.lockedGold = task.wageGold;
            return true;
        }

        return true;
    }

    public void SettleSuccess(TaskEscrowReservation reservation, TreasuryService treasury)
    {
        if (!HasActiveReservation(reservation) || treasury == null)
            return;

        if (reservation.usesLegacyGold && reservation.lockedGold > 0)
            treasury.ConsumeLockedGold(reservation.lockedGold);
    }

    public void SettleFailure(TaskEscrowReservation reservation, TreasuryService treasury)
    {
        if (!HasActiveReservation(reservation) || treasury == null)
            return;

        if (reservation.usesLegacyGold && reservation.lockedGold > 0)
        {
            treasury.RefundGold(reservation.lockedGold);
            return;
        }

        if (reservation.usesBundleCost &&
            reservation.spentBundle != null &&
            !reservation.spentBundle.IsEmpty)
        {
            treasury.GrantBundle(reservation.spentBundle, "task_upfront_cost_refund");
        }
    }

    public void SettleDeath(TaskEscrowReservation reservation, TreasuryService treasury)
    {
        if (!HasActiveReservation(reservation) || treasury == null)
            return;

        if (reservation.usesLegacyGold && reservation.lockedGold > 0)
            treasury.ConsumeLockedGold(reservation.lockedGold);

        // bundle upfront cost stays consumed on death
    }

    public void SettleLost(TaskEscrowReservation reservation, TreasuryService treasury)
    {
        if (!HasActiveReservation(reservation) || treasury == null)
            return;

        if (reservation.usesLegacyGold && reservation.lockedGold > 0)
            treasury.ConsumeLockedGold(reservation.lockedGold);

        // bundle upfront cost stays consumed on lost
    }

    public bool HasActiveReservation(TaskEscrowReservation reservation)
    {
        return reservation != null && reservation.HasAnyReservation;
    }
}