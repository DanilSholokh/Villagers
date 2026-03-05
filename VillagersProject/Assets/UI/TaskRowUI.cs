using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TaskRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text slotsText;
    [SerializeField] private TMP_Text prioText;
    [SerializeField] private Toggle activeToggle;
    [SerializeField] private Button prioPlusBtn;
    [SerializeField] private Button prioMinusBtn;

    [SerializeField] private TMP_Text successText;
    [SerializeField] private Image riskIcon;

    private string taskId;

    public void Bind(TaskInstance task)
    {

        if (prioPlusBtn != null) prioPlusBtn.onClick.AddListener(OnPlus);
        if (prioMinusBtn != null) prioMinusBtn.onClick.AddListener(OnMinus);
        if (activeToggle != null) activeToggle.onValueChanged.AddListener(OnToggle);

        // Success %
        int percent = Mathf.RoundToInt(task.successChance * 100f);
        successText.text = percent + "%";

        // Risk icon (поки просто колір)
        switch (task.riskTier)
        {
            case 0: riskIcon.color = Color.green; break;
            case 1: riskIcon.color = new Color(0.6f, 1f, 0f); break;
            case 2: riskIcon.color = Color.yellow; break;
            case 3: riskIcon.color = new Color(1f, 0.6f, 0f); break;
            case 4: riskIcon.color = new Color(1f, 0.3f, 0f); break;
            case 5: riskIcon.color = Color.red; break;
        }


        Refresh(true);
    }

    public void Refresh(bool force = false)
    {
        var board = GameInstaller.TaskBoard;
        if (board == null) return;

        var t = FindTask(taskId);
        if (t == null) return;

        if (titleText != null)
            titleText.text = $"{t.displayName}";

        if (prioText != null)
            prioText.text = $"{t.priority}";

        if (slotsText != null)
            slotsText.text = $"{board.SlotsTaken(t.taskId)}/{t.maxTakers}";

        if (activeToggle != null && (force || activeToggle.isOn != t.active))
            activeToggle.SetIsOnWithoutNotify(t.active);
    }

    private TaskInstance FindTask(string id)
    {
        var tasks = GameInstaller.TaskBoard.GetAllTasks();
        for (int i = 0; i < tasks.Count; i++)
            if (tasks[i].taskId == id) return tasks[i];
        return null;
    }

    private void OnPlus()
    {
        var t = FindTask(taskId);
        if (t == null) return;
        t.priority = Mathf.Clamp(t.priority + 1, 0, 5);
        Refresh(true);
    }

    private void OnMinus()
    {
        var t = FindTask(taskId);
        if (t == null) return;
        t.priority = Mathf.Clamp(t.priority - 1, 0, 5);
        Refresh(true);
    }

    private void OnToggle(bool value)
    {
        var t = FindTask(taskId);
        if (t == null) return;
        t.active = value;
        Refresh(true);
    }
}
