using System.Collections.Generic;
using UnityEngine;

public class TaskBoardUI : MonoBehaviour
{
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private TaskRowUI rowPrefab;

    private readonly List<TaskRowUI> rows = new();

    private void Start()
    {
        Rebuild();
    }

    private void OnEnable()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        if (rowsRoot == null || rowPrefab == null) return;
        if (GameInstaller.TaskBoard == null) return;

        // clear
        for (int i = 0; i < rows.Count; i++)
            if (rows[i] != null) Destroy(rows[i].gameObject);
        rows.Clear();

        var tasks = GameInstaller.TaskBoard.GetAllTasks();
        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            var row = Instantiate(rowPrefab, rowsRoot);
            row.Bind(t.taskId);
            rows.Add(row);
        }
    }

    private void Update()
    {
        if (GameInstaller.TaskBoard == null) return;

        for (int i = 0; i < rows.Count; i++)
            rows[i].Refresh();
    }
}
