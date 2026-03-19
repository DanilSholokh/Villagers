using System.Collections.Generic;
using UnityEngine;

public class TaskBoardUI : MonoBehaviour
{
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private TaskRowUI rowPrefab;

    [SerializeField] private TMPro.TextMeshProUGUI payloadText;
    [SerializeField] private TMPro.TextMeshProUGUI metaText;

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
        RefreshSummary();
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
        RefreshSummary();
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
        RefreshSummary();
    }

    private void HandleReservationsChanged()
    {
        RefreshAll();
        RefreshSummary();
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

    private void RefreshSummary()
    {
        if (_board == null)
        {
            if (payloadText != null) payloadText.text = "Payload: -";
            if (metaText != null) metaText.text = "Meta: -";
            return;
        }

        var tasks = _board.GetAllTasks();
        if (tasks == null || tasks.Count == 0)
        {
            if (payloadText != null) payloadText.text = "Payload: no tasks";
            if (metaText != null) metaText.text = "Meta: board empty";
            return;
        }

        int gatherCount = 0;
        int exploreCount = 0;
        int surveyCount = 0;
        int totalRewardGold = 0;
        int totalReservedSlots = 0;

        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            if (t == null)
                continue;

            switch (t.type)
            {
                case TaskType.Gather: gatherCount++; break;
                case TaskType.ExploreNewLocation: exploreCount++; break;
                case TaskType.SurveyKnownLocation: surveyCount++; break;
            }

            var reward = t.GetResolvedTaskRewardBundle();
            if (reward != null)
                totalRewardGold += reward.GetExactAmount("gold");

            totalReservedSlots += _board.SlotsTaken(t.taskId);
        }

        if (payloadText != null)
        {
            payloadText.text =
                $"Tasks: {tasks.Count} | Gather: {gatherCount} | Explore: {exploreCount} | Survey: {surveyCount}";
        }

        if (metaText != null)
        {
            metaText.text =
                $"Reserved: {totalReservedSlots} | Total Gold Reward: {totalRewardGold}";
        }
    }


}