using TMPro;
using UnityEngine;

public class VillagerRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI taskText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI perksText;

    public string AgentId { get; private set; }

    public void Bind(string agentId)
    {
        AgentId = agentId;
        Refresh(GameInstaller.Villagers, GameInstaller.Progression);
    }

    public void Refresh(VillagerRosterService roster, ProgressionService prog)
    {
        if (string.IsNullOrWhiteSpace(AgentId)) return;


        var s = roster?.GetOrCreate(AgentId);
        var p = prog?.Get(AgentId);

        if (nameText) nameText.text = s != null ? s.displayName : AgentId;

        if (statusText) statusText.text = s != null ? s.status.ToString() : "Unknown";

        if (taskText)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.taskName))
                taskText.text = "-";
            else
                taskText.text = $"{s.taskName}";
        }

        if (levelText) levelText.text = p != null ? $"Lv.{p.level} (AP:{p.achievementPoints})" : "Lv.?";

        if (statsText)
        {
            if (p == null) statsText.text = "STR- SPD- ING-";
            else statsText.text = $"STR {p.strength} | SPD {p.speed} | ING {p.ingenuity}";
        }

        if (perksText)
        {
            if (p == null || p.perks == null || p.perks.Count == 0) perksText.text = "(no perks)";
            else perksText.text = string.Join(", ", p.perks);
        }
    }
}