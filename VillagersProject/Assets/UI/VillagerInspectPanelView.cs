using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VillagerInspectPanelView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI perksText;
    [SerializeField] private TextMeshProUGUI inventoryText;

    [Header("3D Preview")]
    [SerializeField] private RawImage previewImage;       // показує RenderTexture
    [SerializeField] private Camera previewCamera;        // рендерить тільки preview layer
    [SerializeField] private Transform previewRoot;       // куди інстансити клон
    [SerializeField] private LayerMask previewLayer;      // наприклад "UIPreview"
    [SerializeField] private Vector3 previewLocalPos = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 previewLocalEuler = new Vector3(0, 180, 0);

    private VillagerAgentBrain _target;
    private GameObject _previewInstance;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
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

    private void BuildPreviewModel()
    {
        ClearPreviewModel();

        if (_target == null) return;
        if (previewRoot == null || previewCamera == null || previewImage == null) return;

        // Беремо візуал з віліджера: найпростіше — його MeshRenderer корінь
        var render = _target.GetComponentInChildren<Renderer>();
        if (render == null) return;

        _previewInstance = Instantiate(render.gameObject, previewRoot);
        _previewInstance.transform.localPosition = previewLocalPos;
        _previewInstance.transform.localRotation = Quaternion.Euler(previewLocalEuler);

        // Весь клон — в preview layer, щоб камера бачила тільки його
        int layer = LayerMaskToLayer(previewLayer);
        SetLayerRecursive(_previewInstance.transform, layer);

        previewCamera.cullingMask = previewLayer;

        // Підключення RenderTexture (зазвичай ти створиш RT ассет і закинеш у RawImage)
        // Якщо RT заданий на RawImage — то ок, якщо ні, треба призначити previewCamera.targetTexture вручну.
        // (Безпечно: якщо RawImage.texture є RenderTexture — підключимо до camera)
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