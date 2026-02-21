using UnityEngine;

public class TaskPublisherPanel : MonoBehaviour
{
    [SerializeField] private TaskBoardUI boardUiToRebuild;

    private int _seq = 0;

    public void PublishGatherWood() => PublishGather("wood", "Gather Wood", priority: 3, maxTakers: 2, durationSec: 8f, baseAmount: 3);
    public void PublishGatherStone() => PublishGather("stone", "Gather Stone", priority: 5, maxTakers: 2, durationSec: 10f, baseAmount: 20);
    public void PublishGatherFish() => PublishGather("fish", "Gather Fish", priority: 3, maxTakers: 2, durationSec: 7f, baseAmount: 2);

    public void PublishExplore()
    {
        var board = GameInstaller.TaskBoard;
        if (board == null) return;

        _seq++;

        var task = new TaskInstance
        {
            taskId = $"rt_explore_{_seq}",
            type = TaskType.Explore,
            displayName = $"Explore #{_seq}",
            active = true,
            priority = 2,
            maxTakers = 1,
            durationSec = 6f,
            resourceId = null,
            baseAmount = 0
        };

        board.AddTaskRuntime(task);
        if (boardUiToRebuild != null) boardUiToRebuild.Rebuild();
    }

    private void PublishGather(string resId, string name, int priority, int maxTakers, float durationSec, int baseAmount)
    {
        var board = GameInstaller.TaskBoard;
        if (board == null) return;

        _seq++;

        var task = new TaskInstance
        {
            taskId = $"rt_gather_{resId}_{_seq}",
            type = TaskType.Gather,
            displayName = $"{name} #{_seq}",
            active = true,
            priority = priority,
            maxTakers = maxTakers,
            durationSec = durationSec,
            resourceId = resId,
            baseAmount = baseAmount
        };

        board.AddTaskRuntime(task);
        if (boardUiToRebuild != null) boardUiToRebuild.Rebuild();
    }
}