using System.Collections.Generic;
using System.Linq;

public static class TaskSelectionLogic
{
    public static TaskInstance PickBest(TaskBoardService board)
    {
        var tasks = board.GetAllTasks();

        var available = tasks
            .Where(board.IsAvailable)
            .OrderByDescending(t => t.priority)
            .ThenByDescending(t => board.SlotsFree(t.taskId, t.maxTakers))
            .ThenBy(t => t.durationSec)
            .ToList();

        return available.Count > 0 ? available[0] : null;
    }
}
