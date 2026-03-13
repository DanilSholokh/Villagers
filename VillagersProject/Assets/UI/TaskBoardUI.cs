using System.Collections.Generic;
using UnityEngine;

public class TaskBoardUI : MonoBehaviour
{
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private TaskRowUI rowPrefab;

    private readonly List<TaskRowUI> rows = new();

    private TaskBoardService _board;

    public void Bind(TaskBoardService board)
    {
        // відписка від старого board
        if (_board != null)
        {
            _board.OnTasksChanged -= HandleTasksChanged;
            _board.OnReservationsChanged -= HandleReservationsChanged;
        }

        _board = board;

        // підписка на новий board
        if (_board != null)
        {
            _board.OnTasksChanged -= HandleTasksChanged;               // safety від дублю
            _board.OnTasksChanged += HandleTasksChanged;

            _board.OnReservationsChanged -= HandleReservationsChanged; // safety від дублю
            _board.OnReservationsChanged += HandleReservationsChanged;
        }

        Rebuild();
        RefreshAll();
    }

    private void OnEnable()
    {

        // якщо панель вимикали/вмикали — перепідписатись
        if (_board != null)
        {
            _board.OnTasksChanged -= HandleTasksChanged;               // safety
            _board.OnTasksChanged += HandleTasksChanged;

            _board.OnReservationsChanged -= HandleReservationsChanged; // safety
            _board.OnReservationsChanged += HandleReservationsChanged;
        }

        Rebuild();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (_board != null)
        {
            _board.OnTasksChanged -= HandleTasksChanged;
            _board.OnReservationsChanged -= HandleReservationsChanged;
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
        if (_board == null) return;

        // clear
        for (int i = 0; i < rows.Count; i++)
            if (rows[i] != null) Destroy(rows[i].gameObject);
        rows.Clear();

        var tasks = _board.GetAllTasks();
        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            var row = Instantiate(rowPrefab, rowsRoot);
            row.Bind(t);
            rows.Add(row);
        }
    }

    private void RefreshAll()
    {
        if (_board == null) return;

        for (int i = 0; i < rows.Count; i++)
            if (rows[i] != null) rows[i].Refresh();
    }
}