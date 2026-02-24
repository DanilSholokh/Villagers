using System.Collections.Generic;
using UnityEngine;

public class TaskBoardUI : MonoBehaviour
{
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private TaskRowUI rowPrefab;

    private readonly List<TaskRowUI> rows = new();

    private TaskBoardService Board => GameInstaller.TaskBoard;

    private void OnEnable()
    {
        if (Board != null)
        {
            Board.OnTasksChanged += HandleTasksChanged;
            Board.OnReservationsChanged += HandleReservationsChanged;
        }

        Rebuild();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (Board != null)
        {
            Board.OnTasksChanged -= HandleTasksChanged;
            Board.OnReservationsChanged -= HandleReservationsChanged;
        }
    }

    private void HandleTasksChanged()
    {
        Rebuild();
        RefreshAll();
    }

    private void HandleReservationsChanged()
    {
        RefreshAll();
    }

    public void Rebuild()
    {
        if (rowsRoot == null || rowPrefab == null) return;
        if (Board == null) return;

        // clear
        for (int i = 0; i < rows.Count; i++)
            if (rows[i] != null) Destroy(rows[i].gameObject);
        rows.Clear();

        var tasks = Board.GetAllTasks();
        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            var row = Instantiate(rowPrefab, rowsRoot);
            row.Bind(t.taskId);
            rows.Add(row);
        }
    }

    private void RefreshAll()
    {
        if (Board == null) return;

        for (int i = 0; i < rows.Count; i++)
            if (rows[i] != null) rows[i].Refresh();
    }
}