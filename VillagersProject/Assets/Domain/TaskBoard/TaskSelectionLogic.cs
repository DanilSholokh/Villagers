using System.Linq;

public static class TaskSelectionLogic
{
    public static TaskInstance PickBest(TaskBoardService board, TreasuryService treasury)
    {
        if (board == null)
            return null;

        var tasks = board.GetAllTasks();

        var available = tasks
            .Where(t => t != null)
            .Where(board.IsAvailable)
            .Where(t => CanStartTask(t, treasury))
            .OrderByDescending(t => t.priority)
            .ThenByDescending(t => board.SlotsFree(t.taskId, t.maxTakers))
            .ThenBy(t => t.durationSec)
            .ToList();

        return available.Count > 0 ? available[0] : null;
    }

    private static bool CanStartTask(TaskInstance task, TreasuryService treasury)
    {
        if (task == null)
            return false;

        if (treasury == null)
            return true;

        var costBundle = task.GetResolvedTaskCostBundle();
        if (costBundle != null && !costBundle.IsEmpty)
            return treasury.CanAfford(costBundle);

        if (task.wageGold > 0)
            return treasury.GetAmount("gold") >= task.wageGold;

        return true;
    }
}