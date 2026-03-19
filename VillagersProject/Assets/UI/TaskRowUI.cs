using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class TaskRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text slotsText;
    [SerializeField] private TMP_Text prioText;
    [SerializeField] private TMP_Text successText;
    [SerializeField] private TMP_Text payloadText;
    [SerializeField] private TMP_Text metaText;

    [SerializeField] private Toggle activeToggle;
    [SerializeField] private Button prioPlusBtn;
    [SerializeField] private Button prioMinusBtn;
    [SerializeField] private Image riskIcon;

    private string taskId;

    public void Bind(TaskInstance task)
    {
        if (task == null)
            return;

        taskId = task.taskId;

        if (prioPlusBtn != null)
        {
            prioPlusBtn.onClick.RemoveListener(OnPlus);
            prioPlusBtn.onClick.AddListener(OnPlus);
        }

        if (prioMinusBtn != null)
        {
            prioMinusBtn.onClick.RemoveListener(OnMinus);
            prioMinusBtn.onClick.AddListener(OnMinus);
        }

        if (activeToggle != null)
        {
            activeToggle.onValueChanged.RemoveListener(OnToggle);
            activeToggle.onValueChanged.AddListener(OnToggle);
        }

        Refresh(true);
    }

    public void Refresh(bool force = false)
    {
        var board = GameInstaller.TaskBoard;
        if (board == null)
            return;

        var task = FindTask(taskId);
        if (task == null)
            return;

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(task.displayName) ? task.taskId : task.displayName;

        if (slotsText != null)
        {
            int maxTakers = Mathf.Max(1, task.maxTakers);
            int freeSlots = board.SlotsFree(task.taskId, task.maxTakers);
            int reserved = Mathf.Clamp(maxTakers - freeSlots, 0, maxTakers);
            slotsText.text = $"{reserved}/{maxTakers}";
        }

        if (prioText != null)
            prioText.text = $"P{task.priority}";

        if (activeToggle != null)
            activeToggle.SetIsOnWithoutNotify(task.active);

        if (successText != null)
        {
            int percent = Mathf.RoundToInt(task.successChance * 100f);
            successText.text = percent + "%";
        }

        RefreshRisk(task);

        if (payloadText != null)
        {
            string payload = EconomyUiTextFormatter.BuildTaskPayloadSummary(task);
            payloadText.text = string.IsNullOrWhiteSpace(payload)
                ? "Output: -\nReward: -\nCost: -"
                : payload;
        }

        if (metaText != null)
            metaText.text = BuildMetaText(task);
    }

    private string BuildMetaText(TaskInstance task)
    {
        var sb = new StringBuilder(192);

        sb.Append("Type: ").Append(task.type).Append('\n');
        sb.Append("Duration: ").Append(task.durationSec.ToString("0.0")).Append("s").Append('\n');
        sb.Append("Risk Tier: ").Append(task.riskTier).Append('\n');

        string targetLocationId = task.GetResolvedTargetLocationId();
        if (!string.IsNullOrWhiteSpace(targetLocationId))
            sb.Append("Target: ").Append(GetLocationDisplayName(targetLocationId));
        else
            sb.Append("Target: random suitable location");

        return sb.ToString();
    }

    private string GetLocationDisplayName(string locationId)
    {
        if (string.IsNullOrWhiteSpace(locationId))
            return "-";

        var locations = GameInstaller.LocationService;
        if (locations == null)
            return locationId;

        var loc = locations.GetLocation(locationId);
        if (loc == null)
            return locationId;

        return string.IsNullOrWhiteSpace(loc.name)
            ? loc.id
            : loc.name;
    }

    private void RefreshRisk(TaskInstance task)
    {
        if (riskIcon == null || task == null)
            return;

        switch (task.riskTier)
        {
            case 0: riskIcon.color = Color.green; break;
            case 1: riskIcon.color = new Color(0.6f, 1f, 0f); break;
            case 2: riskIcon.color = Color.yellow; break;
            case 3: riskIcon.color = new Color(1f, 0.6f, 0f); break;
            case 4: riskIcon.color = new Color(1f, 0.3f, 0f); break;
            default: riskIcon.color = Color.red; break;
        }
    }

    private TaskInstance FindTask(string id)
    {
        var board = GameInstaller.TaskBoard;
        if (board == null || string.IsNullOrWhiteSpace(id))
            return null;

        var all = board.GetAllTasks();
        for (int i = 0; i < all.Count; i++)
        {
            var task = all[i];
            if (task != null && task.taskId == id)
                return task;
        }

        return null;
    }

    private void OnPlus()
    {
        var board = GameInstaller.TaskBoard;
        var task = FindTask(taskId);
        if (board == null || task == null)
            return;

        task.priority += 1;
        Refresh();
    }

    private void OnMinus()
    {
        var board = GameInstaller.TaskBoard;
        var task = FindTask(taskId);
        if (board == null || task == null)
            return;

        task.priority = Mathf.Max(0, task.priority - 1);
        Refresh();
    }

    private void OnToggle(bool value)
    {
        var task = FindTask(taskId);
        if (task == null)
            return;

        task.active = value;
        Refresh();
    }
}