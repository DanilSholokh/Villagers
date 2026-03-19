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
            return true;

        var upfrontCostBundle = task.GetResolvedTaskCostBundle();
        if (upfrontCostBundle == null || upfrontCostBundle.IsEmpty)
            return true;

        var validateResult = treasury.ValidateBundle(upfrontCostBundle, "task_upfront_cost_preview");
        if (!validateResult.success)
        {
            errorMessage = string.IsNullOrWhiteSpace(validateResult.message)
                ? $"Not enough upfront bundle cost for task={task.taskId}"
                : validateResult.message;
            return false;
        }

        var spendResult = treasury.SpendBundle(upfrontCostBundle, "task_upfront_cost");
        if (!spendResult.success)
        {
            errorMessage = $"Upfront bundle spend failed for task={task.taskId} reason={spendResult.message}";
            return false;
        }

        reservation.usesBundleCost = true;
        reservation.usesLegacyGold = false;
        reservation.lockedGold = 0;
        reservation.spentBundle = spendResult.consumed != null && !spendResult.consumed.IsEmpty
            ? spendResult.consumed.Clone()
            : upfrontCostBundle.Clone();

        return true;
    }

    public void SettleSuccess(TaskEscrowReservation reservation, TreasuryService treasury)
    {
        if (!HasActiveReservation(reservation) || treasury == null)
            return;

        ClearReservation(reservation);
    }

    public void SettleFailure(TaskEscrowReservation reservation, TreasuryService treasury)
    {
        if (!HasActiveReservation(reservation) || treasury == null)
            return;

        if (reservation.usesBundleCost &&
            reservation.spentBundle != null &&
            !reservation.spentBundle.IsEmpty)
        {
            treasury.GrantBundle(reservation.spentBundle, "task_upfront_cost_refund");
        }

        ClearReservation(reservation);
    }

    public void SettleDeath(TaskEscrowReservation reservation, TreasuryService treasury)
    {
        if (!HasActiveReservation(reservation) || treasury == null)
            return;

        // upfront bundle cost stays consumed on death
        ClearReservation(reservation);
    }

    public void SettleLost(TaskEscrowReservation reservation, TreasuryService treasury)
    {
        if (!HasActiveReservation(reservation) || treasury == null)
            return;

        // upfront bundle cost stays consumed on lost
        ClearReservation(reservation);
    }

    public bool HasActiveReservation(TaskEscrowReservation reservation)
    {
        return reservation != null && reservation.HasAnyReservation;
    }

    private void ClearReservation(TaskEscrowReservation reservation)
    {
        reservation?.Clear();
    }
}