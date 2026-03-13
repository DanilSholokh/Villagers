using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VillagerInspectPanelView : MonoBehaviour
{

    private string _selectedAgentId;

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI perksText;
    [SerializeField] private TextMeshProUGUI inventoryText;


    [Header("Auto Refresh")]
    [SerializeField] private bool autoRefresh = true;            // <- ДОДАЙ
    [SerializeField] private float refreshInterval = 0.25f;

    [Header("3D Preview")]
    [SerializeField] private RawImage previewImage;       // показує RenderTexture
    [SerializeField] private Camera previewCamera;        // рендерить тільки preview layer
    [SerializeField] private Transform previewRoot;       // куди інстансити клон
    [SerializeField] private LayerMask previewLayer;      // наприклад "UIPreview"
    [SerializeField] private Vector3 previewLocalPos = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 previewLocalEuler = new Vector3(0, 180, 0);

    private VillagerAgentBrain _target;
    private GameObject _previewInstance;

    private float _nextRefreshAt;
    private string _lastSnapshot; // щоб не перерисовувати без змін

    private void Awake()
    {
        if (root != null) root.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    private void Update()
    {
        if (!autoRefresh) return;
        if (_target == null) return;
        if (Time.unscaledTime < _nextRefreshAt) return;

        _nextRefreshAt = Time.unscaledTime + refreshInterval;
        RebuildAll_IfChanged();
    }

    private void OnEnable()
    {
        if (GameInstaller.SelectedVillager != null)
            GameInstaller.SelectedVillager.OnSelectedChanged += HandleSelectedChanged;

        if (GameInstaller.Villagers != null)
            GameInstaller.Villagers.OnVillagerChanged += HandleVillagerChanged;

        if (GameInstaller.Progression != null)
            GameInstaller.Progression.OnProgressChanged += HandleProgressChanged;

        RefreshFromService();
    }

    private void OnDisable()
    {
        if (GameInstaller.SelectedVillager != null)
            GameInstaller.SelectedVillager.OnSelectedChanged -= HandleSelectedChanged;

        if (GameInstaller.Villagers != null)
            GameInstaller.Villagers.OnVillagerChanged -= HandleVillagerChanged;

        if (GameInstaller.Progression != null)
            GameInstaller.Progression.OnProgressChanged -= HandleProgressChanged;
    }

    private void OnDestroy()
    {
        if (GameInstaller.SelectedVillager != null)
            GameInstaller.SelectedVillager.OnSelectedChanged -= HandleSelectedChanged;

        if (GameInstaller.Villagers != null)
            GameInstaller.Villagers.OnVillagerChanged -= HandleVillagerChanged;

        if (GameInstaller.Progression != null)
            GameInstaller.Progression.OnProgressChanged -= HandleProgressChanged;
    }

    public void Show(VillagerAgentBrain brain)
    {
        if (brain == null) return;

        _target = brain;

        if (root != null) root.SetActive(true);

        _nextRefreshAt = 0f;
        _lastSnapshot = null;

        RebuildAll();
        BuildPreviewModel();
    }

    public void Hide()
    {
        _target = null;
        if (root != null) root.SetActive(false);
        ClearPreviewModel();
    }




    private void RebuildAll_IfChanged()
    {
        // Знімаємо “знімок” даних з сервісів.
        // Якщо нічого не змінилось — не чіпаємо TMP/лейаути.
        var snap = BuildSnapshotString();
        if (snap == _lastSnapshot) return;

        _lastSnapshot = snap;
        RebuildAll();
    }


    private string BuildSnapshotString()
    {
        if (_target == null) return "";

        var agentId = _target.AgentId;
        var roster = GameInstaller.Villagers;
        var prog = GameInstaller.Progression;
        var inv = GameInstaller.Inventory != null ? GameInstaller.Inventory.Get(agentId) : null;
        var cargo = _target.GetCargoSnapshot();

        var s = roster?.GetOrCreate(agentId);
        var p = prog?.Get(agentId);

        var sb = new StringBuilder(256);
        sb.Append(agentId).Append('|');
        sb.Append(s != null ? s.status.ToString() : "null").Append('|');
        sb.Append(s != null ? s.taskName : "null").Append('|');
        sb.Append(s != null ? s.taskId : "null").Append('|');
        sb.Append(p != null ? p.level.ToString() : "null").Append('|');

        if (p != null && p.perks != null)
            sb.Append(string.Join(",", p.perks));
        sb.Append('|');

        if (cargo != null && cargo.Count > 0)
        {
            foreach (var kv in cargo)
                sb.Append(kv.Key).Append(':').Append(kv.Value).Append(',');
        }
        sb.Append('|');

        if (inv != null && inv.Count > 0)
        {
            for (int i = 0; i < inv.Count; i++)
                sb.Append(inv[i].itemId).Append(':').Append(inv[i].amount).Append(',');
        }

        return sb.ToString();
    }

    private void RebuildAll()
    {
        if (_target == null) return;

        var agentId = _target.AgentId;

        var roster = GameInstaller.Villagers;
        var prog = GameInstaller.Progression;

        var s = roster?.GetOrCreate(agentId);
        var p = prog?.Get(agentId);

        if (titleText != null)
            titleText.text = s != null ? s.displayName : agentId;

        if (statusText != null)
            statusText.text = BuildStatusText(s);

        if (statsText != null)
            statsText.text = BuildStatsText(p);

        if (perksText != null)
            perksText.text = BuildPerksText(p);

        RebuildInventory(_target);
    }


    // ----- 3D Preview (твій код лишаємо) -----

    private void BuildPreviewModel()
    {
        ClearPreviewModel();

        if (_target == null) return;
        if (previewRoot == null || previewCamera == null || previewImage == null) return;

        var render = _target.GetComponentInChildren<Renderer>();
        if (render == null) return;

        _previewInstance = Instantiate(render.gameObject, previewRoot);
        _previewInstance.transform.localPosition = previewLocalPos;
        _previewInstance.transform.localRotation = Quaternion.Euler(previewLocalEuler);

        int layer = LayerMaskToLayer(previewLayer);
        SetLayerRecursive(_previewInstance.transform, layer);

        previewCamera.cullingMask = previewLayer;

        if (previewImage.texture is RenderTexture rt)
            previewCamera.targetTexture = rt;
    }


    private string BuildStatusText(VillagerState state)
    {
        if (state == null)
            return "No villager state.";

        string updated = state.lastChangeUtc.ToLocalTime().ToString("HH:mm:ss");

        var sb = new StringBuilder(128);
        sb.AppendLine($"Status: {state.status}");
        sb.AppendLine($"Task: {Safe(state.taskName)}");
        sb.AppendLine($"TaskId: {Safe(state.taskId)}");
        sb.AppendLine($"Updated: {updated}");

        return sb.ToString().TrimEnd();
    }

    private string BuildStatsText(ProgressionService.VillagerProgress prog)
    {
        if (prog == null)
            return "No progression data.";

        var sb = new StringBuilder(128);
        sb.AppendLine($"Level: {prog.level}");
        sb.AppendLine($"AP: {prog.achievementPoints}");
        sb.AppendLine($"STR: {prog.strength}");
        sb.AppendLine($"SPD: {prog.speed}");
        sb.AppendLine($"ING: {prog.ingenuity}");

        return sb.ToString().TrimEnd();
    }

    private string BuildPerksText(ProgressionService.VillagerProgress prog)
    {
        if (prog == null || prog.perks == null || prog.perks.Count == 0)
            return "Perks: -";

        return "Perks: " + string.Join(", ", prog.perks);
    }

    private void RebuildInventory(VillagerAgentBrain brain)
    {
        if (inventoryText == null)
            return;

        string agentId = brain.AgentId;
        var inv = GameInstaller.Inventory != null ? GameInstaller.Inventory.Get(agentId) : null;
        var cargo = brain.GetCargoSnapshot();

        var sb = new StringBuilder(256);

        sb.AppendLine("Cargo:");
        if (cargo == null || cargo.Count == 0)
        {
            sb.AppendLine("-");
        }
        else
        {
            foreach (var kv in cargo)
                sb.AppendLine($"{kv.Key} x{kv.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("Inventory:");

        if (inv == null || inv.Count == 0)
        {
            sb.AppendLine("-");
        }
        else
        {
            foreach (var item in inv)
                sb.AppendLine($"{item.itemId} x{item.amount}");
        }

        inventoryText.text = sb.ToString().TrimEnd();
    }

    private void ClearPreviewModel()
    {
        if (_previewInstance != null)
            Destroy(_previewInstance);
        _previewInstance = null;
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i), layer);
    }

    private static int LayerMaskToLayer(LayerMask mask)
    {
        int v = mask.value;
        for (int i = 0; i < 32; i++)
            if ((v & (1 << i)) != 0) return i;
        return 0;
    }


    private void HandleSelectedChanged(string agentId)
    {
        _selectedAgentId = agentId;
        RefreshFromService();
    }

    private void HandleVillagerChanged(string agentId)
    {
        if (!string.IsNullOrWhiteSpace(_selectedAgentId) && _selectedAgentId == agentId)
            RefreshFromService();
    }

    private void HandleProgressChanged(string agentId)
    {
        if (!string.IsNullOrWhiteSpace(_selectedAgentId) && _selectedAgentId == agentId)
            RefreshFromService();
    }

    private void RefreshFromService()
    {
        _selectedAgentId = GameInstaller.SelectedVillager != null
            ? GameInstaller.SelectedVillager.SelectedId
            : _selectedAgentId;

        if (string.IsNullOrWhiteSpace(_selectedAgentId))
        {
            Hide();
            return;
        }

        var brain = FindBrain(_selectedAgentId);
        if (brain == null)
        {
            Hide();
            return;
        }

        Show(brain);
    }

    private VillagerAgentBrain FindBrain(string agentId)
    {
        var brains = FindObjectsByType<VillagerAgentBrain>(FindObjectsSortMode.None);

        for (int i = 0; i < brains.Length; i++)
        {
            if (brains[i] != null && brains[i].AgentId == agentId)
                return brains[i];
        }

        return null;
    }

    private string Safe(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

}