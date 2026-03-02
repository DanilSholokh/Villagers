using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TaskBoardService
{
    private readonly List<TaskInstance> tasks = new();
    private readonly Dictionary<string, HashSet<string>> reservations = new(); // taskId -> agentIds

    // 🔔 UI/іншим системам: структура дошки (список тасків) змінилась
    public event Action OnTasksChanged;

    // 🔔 UI/іншим системам: змінились слоти (reserve/release)
    public event Action OnReservationsChanged;

    public void SetTasks(List<TaskInstance> newTasks)
    {
        tasks.Clear();
        tasks.AddRange(newTasks);

        reservations.Clear();
        foreach (var t in tasks)
            reservations[t.taskId] = new HashSet<string>();

        Debug.Log($"[TaskBoard] Loaded tasks={tasks.Count}");

        OnTasksChanged?.Invoke();
        OnReservationsChanged?.Invoke();
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
            OnReservationsChanged?.Invoke();
            return true;
        }

        return false;
    }

    public void Release(string taskId, string agentId)
    {
        if (!reservations.TryGetValue(taskId, out var set))
            return;

        if (set.Remove(agentId))
        {
            Debug.Log($"[TaskBoard] Release task={taskId} agent={agentId} slots={set.Count}");
            OnReservationsChanged?.Invoke();
        }
    }

    public void AddTaskRuntime(TaskInstance task)
    {
        if (task == null) return;
        if (string.IsNullOrWhiteSpace(task.taskId)) return;

        // якщо taskId існує — оновлюємо дані
        var existing = tasks.FirstOrDefault(x => x.taskId == task.taskId);
        if (existing != null)
        {
            existing.type = task.type;
            existing.displayName = task.displayName;
            existing.active = task.active;
            existing.priority = task.priority;
            existing.maxTakers = task.maxTakers;
            existing.durationSec = task.durationSec;

            existing.wageGold = task.wageGold;

            existing.resourceId = task.resourceId;
            existing.baseAmount = task.baseAmount;

            OnTasksChanged?.Invoke();
            OnReservationsChanged?.Invoke();
            return;
        }

        tasks.Add(task);

        if (!reservations.ContainsKey(task.taskId))
            reservations[task.taskId] = new HashSet<string>();

        Debug.Log($"[TaskBoard] Added runtime task={task.taskId}");

        OnTasksChanged?.Invoke();
        OnReservationsChanged?.Invoke();
    }

    public bool RemoveTaskRuntime(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return false;

        var t = tasks.FirstOrDefault(x => x.taskId == taskId);
        if (t == null) return false;

        // не видаляємо, якщо хтось ще зарезервив
        if (reservations.TryGetValue(taskId, out var set) && set.Count > 0)
            return false;

        tasks.Remove(t);
        reservations.Remove(taskId);

        Debug.Log($"[TaskBoard] Removed runtime task={taskId}");

        OnTasksChanged?.Invoke();
        OnReservationsChanged?.Invoke();
        return true;
    }

}