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
        if (_board != null)
        {
            _board.OnTasksChanged -= HandleTasksChanged;
            _board.OnReservationsChanged -= HandleReservationsChanged;
        }

        _board = board;

        if (_board != null)
        {
            _board.OnTasksChanged -= HandleTasksChanged;
            _board.OnTasksChanged += HandleTasksChanged;

            _board.OnReservationsChanged -= HandleReservationsChanged;
            _board.OnReservationsChanged += HandleReservationsChanged;
        }

        Rebuild();
        RefreshAll();
        RefreshSummary();
    }

    private void OnEnable()
    {
        if (_board != null)
        {
            _board.OnTasksChanged -= HandleTasksChanged;
            _board.OnTasksChanged += HandleTasksChanged;

            _board.OnReservationsChanged -= HandleReservationsChanged;
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
        if (rowsRoot == null || rowPrefab == null || _board == null)
            return;

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null)
                Destroy(rows[i].gameObject);
        }

        rows.Clear();

        var tasks = _board.GetAllTasks();
        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            if (t == null)
                continue;

            var row = Instantiate(rowPrefab, rowsRoot);
            row.Bind(t);
            rows.Add(row);
        }
    }

    private void RefreshAll()
    {
        if (_board == null)
            return;

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null)
                rows[i].Refresh();
        }
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
        int totalReservedSlots = 0;

        var totalOutput = new ResourceBundle();
        var totalReward = new ResourceBundle();
        var totalCost = new ResourceBundle();

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

            totalReservedSlots += _board.SlotsTaken(t.taskId);
            MergeInto(totalOutput, t.GetResolvedWorkOutputBundle());
            MergeInto(totalReward, t.GetResolvedTaskRewardBundle());
            MergeInto(totalCost, t.GetResolvedTaskCostBundle());
        }

        if (payloadText != null)
        {
            payloadText.text =
                EconomyUiTextFormatter.FormatBundleLine("Output", totalOutput) + "\n" +
                EconomyUiTextFormatter.FormatBundleLine("Reward", totalReward) + "\n" +
                EconomyUiTextFormatter.FormatBundleLine("Cost", totalCost);
        }

        if (metaText != null)
        {
            metaText.text =
                $"Tasks: {tasks.Count} | Gather: {gatherCount} | Explore: {exploreCount} | Survey: {surveyCount}\n" +
                $"Reserved: {totalReservedSlots}";
        }
    }

    private void MergeInto(ResourceBundle target, ResourceBundle source)
    {
        if (target == null || source == null || source.IsEmpty)
            return;

        var exact = source.ExactResources;
        if (exact != null)
        {
            for (int i = 0; i < exact.Count; i++)
            {
                var stack = exact[i];
                if (stack.IsValid)
                    target.AddExact(stack.resourceId, stack.amount);
            }
        }

        var categories = source.CategoryValues;
        if (categories != null)
        {
            for (int i = 0; i < categories.Count; i++)
            {
                var req = categories[i];
                if (req.IsValid)
                    target.AddCategoryValue(req.categoryId, req.value);
            }
        }

        target.Normalize();
    }
}