using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VillagerInspectPanelView : MonoBehaviour
{
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
        if (GameInstaller.Inventory != null)
            GameInstaller.Inventory.OnInventoryChanged += HandleInventoryChanged;
    }

    private void OnDisable()
    {
        if (GameInstaller.Inventory != null)
            GameInstaller.Inventory.OnInventoryChanged -= HandleInventoryChanged;
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

    private void HandleInventoryChanged(string agentId)
    {
        if (_target == null) return;
        if (_target.AgentId != agentId) return;
        RebuildInventory();
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

        var s = roster?.GetOrCreate(agentId);
        var p = prog?.Get(agentId);

        // Мінімальний набір, який реально міняється під час гри:
        // статус/таск/level/перки + можна розширити.
        var sb = new StringBuilder(128);
        sb.Append(agentId).Append('|');
        sb.Append(s != null ? s.status.ToString() : "null").Append('|');
        sb.Append(s != null ? s.taskName : "null").Append('|');
        sb.Append(p != null ? p.level.ToString() : "null").Append('|');
        if (p != null && p.perks != null)
            sb.Append(string.Join(",", p.perks));
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

        if (titleText) titleText.text = s != null ? s.displayName : agentId;

        if (statusText)
        {
            string task = (s == null || string.IsNullOrWhiteSpace(s.taskName)) ? "-" : s.taskName;
            string st = s != null ? s.status.ToString() : "Unknown";
            statusText.text = $"Status: {st}\nTask: {task}";
        }

        if (statsText)
        {
            if (p == null) statsText.text = "Lv.? (AP:?)\nSTR - | SPD - | ING -";
            else statsText.text = $"Lv.{p.level} (AP:{p.achievementPoints})\nSTR {p.strength} | SPD {p.speed} | ING {p.ingenuity}";
        }

        if (perksText)
        {
            if (p == null || p.perks == null || p.perks.Count == 0) perksText.text = "Perks: (none)";
            else perksText.text = "Perks: " + string.Join(", ", p.perks);
        }

        RebuildInventory();
    }

    private void RebuildInventory()
    {
        if (inventoryText == null) return;
        if (_target == null) { inventoryText.text = ""; return; }

        var inv = GameInstaller.Inventory;
        if (inv == null)
        {
            inventoryText.text = "Inventory: (service missing)";
            return;
        }

        var items = inv.Get(_target.AgentId);
        if (items == null || items.Count == 0)
        {
            inventoryText.text = "Inventory: (empty)";
            return;
        }

        var sb = new StringBuilder(128);
        sb.AppendLine("Inventory:");
        for (int i = 0; i < items.Count; i++)
            sb.AppendLine($"- {items[i].itemId} x{items[i].amount}");
        inventoryText.text = sb.ToString();
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

}