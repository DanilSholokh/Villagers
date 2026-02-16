using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TaskBoardService
{
    private readonly List<TaskInstance> tasks = new();
    private readonly Dictionary<string, HashSet<string>> reservations = new(); // taskId -> agentIds

    public void SetTasks(List<TaskInstance> newTasks)
    {
        tasks.Clear();
        tasks.AddRange(newTasks);

        reservations.Clear();
        foreach (var t in tasks)
            reservations[t.taskId] = new HashSet<string>();

        Debug.Log($"[TaskBoard] Loaded tasks={tasks.Count}");
    }

    public IReadOnlyList<TaskInstance> GetAllTasks() => tasks;

    public bool IsAvailable(TaskInstance t)
    {
        if (!t.active) return false;
        if (!reservations.TryGetValue(t.taskId, out var set)) return false;
        return set.Count < Mathf.Max(1, t.maxTakers);
    }

    public int SlotsTaken(string taskId)
        => reservations.TryGetValue(taskId, out var set) ? set.Count : 0;

    public int SlotsFree(string taskId, int maxTakers)
        => Mathf.Max(0, Mathf.Max(1, maxTakers) - SlotsTaken(taskId));

    public bool TryReserve(string taskId, string agentId)
    {
        if (!reservations.TryGetValue(taskId, out var set))
            return false;

        var t = tasks.FirstOrDefault(x => x.taskId == taskId);
        if (t == null || !t.active)
            return false;

        if (set.Count >= Mathf.Max(1, t.maxTakers))
            return false;

        if (set.Add(agentId))
        {
            Debug.Log($"[TaskBoard] Reserve task={taskId} agent={agentId} slots={set.Count}/{t.maxTakers}");
            return true;
        }

        return false;
    }

    public void Release(string taskId, string agentId)
    {
        if (!reservations.TryGetValue(taskId, out var set))
            return;

        if (set.Remove(agentId))
            Debug.Log($"[TaskBoard] Release task={taskId} agent={agentId} slots={set.Count}");
    }
}
