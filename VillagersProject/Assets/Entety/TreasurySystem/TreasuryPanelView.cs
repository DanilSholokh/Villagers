using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TreasuryPanelView : MonoBehaviour
{
    [Header("Dynamic List")]
    [SerializeField] private Transform listRoot;
    [SerializeField] private RectTransform rowTemplate;

    [Header("Optional Category Value Block")]
    [SerializeField] private TMP_Text categoryValuesText;

    [Header("Optional Sell Preview")]
    [SerializeField] private TMP_Text sellPreviewText;

    [Header("Actions")]
    [SerializeField] private Button sellAllButton;

    private TreasuryService _treasury;
    private readonly List<RectTransform> _spawnedRows = new();
    private readonly Dictionary<string, RowRefs> _rowsByKey = new();

    private sealed class RowRefs
    {
        public RectTransform root;
        public TMP_Text keyText;
        public TMP_Text valueText;
    }

    public void Bind(TreasuryService treasury)
    {
        if (_treasury != null)
            _treasury.OnChanged -= HandleChanged;

        _treasury = treasury;

        if (_treasury != null)
            _treasury.OnChanged += HandleChanged;

        if (sellAllButton != null)
        {
            sellAllButton.onClick.RemoveListener(OnSellAllClicked);
            sellAllButton.onClick.AddListener(OnSellAllClicked);
        }

        RebuildRows();
        RefreshAll();
    }

    private void OnEnable()
    {
        if (_treasury != null)
        {
            _treasury.OnChanged -= HandleChanged;
            _treasury.OnChanged += HandleChanged;
        }

        if (sellAllButton != null)
        {
            sellAllButton.onClick.RemoveListener(OnSellAllClicked);
            sellAllButton.onClick.AddListener(OnSellAllClicked);
        }

        RebuildRows();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (_treasury != null)
            _treasury.OnChanged -= HandleChanged;

        if (sellAllButton != null)
            sellAllButton.onClick.RemoveListener(OnSellAllClicked);
    }

    private void HandleChanged(string _, int __)
    {
        RefreshAll();
    }

    private void RebuildRows()
    {
        ClearSpawnedRows();
        _rowsByKey.Clear();

        if (listRoot == null || rowTemplate == null)
            return;

        if (GameInstaller.Resources == null)
            return;

        rowTemplate.gameObject.SetActive(false);

        BuildResourceRows();
        BuildCategoryRows();
    }

    private void BuildResourceRows()
    {
        var allResources = new List<ResourceData>(GameInstaller.Resources.GetAll());
        allResources.Sort((a, b) =>
        {
            if (a == null) return 1;
            if (b == null) return -1;

            int bySort = a.sortOrder.CompareTo(b.sortOrder);
            if (bySort != 0)
                return bySort;

            return string.CompareOrdinal(a.displayName, b.displayName);
        });

        for (int i = 0; i < allResources.Count; i++)
        {
            var resource = allResources[i];
            if (resource == null)
                continue;

            string resourceId = Normalize(resource.resourceId);
            if (string.IsNullOrWhiteSpace(resourceId))
                continue;

            var row = Instantiate(rowTemplate, listRoot);
            row.name = $"Row_Resource_{resourceId}";
            row.gameObject.SetActive(true);

            var refs = BuildRowRefs(row);
            if (refs == null)
            {
                Destroy(row.gameObject);
                continue;
            }

            refs.keyText.text = string.IsNullOrWhiteSpace(resource.displayName)
                ? resourceId
                : resource.displayName;

            refs.valueText.text = "0";

            _spawnedRows.Add(row);
            _rowsByKey[$"resource:{resourceId}"] = refs;
        }
    }

    private void BuildCategoryRows()
    {
        if (GameInstaller.ResourceCategories == null)
            return;

        var categories = new List<ResourceCategoryData>(GameInstaller.ResourceCategories.GetAll());
        categories.Sort((a, b) =>
        {
            if (a == null) return 1;
            if (b == null) return -1;

            return string.CompareOrdinal(a.displayName, b.displayName);
        });

        for (int i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            if (category == null)
                continue;

            string categoryId = Normalize(category.categoryId);
            if (string.IsNullOrWhiteSpace(categoryId))
                continue;

            var row = Instantiate(rowTemplate, listRoot);
            row.name = $"Row_Category_{categoryId}";
            row.gameObject.SetActive(true);

            var refs = BuildRowRefs(row);
            if (refs == null)
            {
                Destroy(row.gameObject);
                continue;
            }

            string categoryName = string.IsNullOrWhiteSpace(category.displayName)
                ? categoryId
                : category.displayName;

            refs.keyText.text = $"{categoryName} Value";
            refs.valueText.text = "0";

            _spawnedRows.Add(row);
            _rowsByKey[$"category:{categoryId}"] = refs;
        }
    }

    private void RefreshAll()
    {
        if (_treasury == null)
            return;

        if (_rowsByKey.Count == 0)
            RebuildRows();

        RefreshResourceRows();
        RefreshCategoryRows();
        RefreshCategoryValuesText();
        RefreshSellPreviewText();
    }

    private void RefreshResourceRows()
    {
        foreach (var pair in _rowsByKey)
        {
            if (!pair.Key.StartsWith("resource:"))
                continue;

            string resourceId = pair.Key.Substring("resource:".Length);
            RowRefs refs = pair.Value;

            if (refs == null || refs.valueText == null)
                continue;

            if (resourceId == "gold")
                refs.valueText.text = _treasury.GetGoldUi();
            else
                refs.valueText.text = _treasury.GetAmount(resourceId).ToString();
        }
    }

    private void RefreshCategoryRows()
    {
        if (GameInstaller.CategoryValues == null || GameInstaller.ResourceCategories == null)
            return;

        foreach (var pair in _rowsByKey)
        {
            if (!pair.Key.StartsWith("category:"))
                continue;

            string categoryId = pair.Key.Substring("category:".Length);
            RowRefs refs = pair.Value;

            if (refs == null || refs.valueText == null)
                continue;

            int value = GameInstaller.CategoryValues.GetContainerCategoryValue(_treasury, categoryId);
            refs.valueText.text = value.ToString();
        }
    }

    private void RefreshCategoryValuesText()
    {
        if (categoryValuesText == null)
            return;

        if (_treasury == null || GameInstaller.CategoryValues == null)
        {
            categoryValuesText.text = "Category Values: -";
            return;
        }

        var values = GameInstaller.CategoryValues.BuildCategoryValueStacks(_treasury);
        categoryValuesText.text = EconomyUiTextFormatter.FormatCategoryValueStacks(values);
    }

    private void RefreshSellPreviewText()
    {
        if (sellPreviewText == null)
            return;

        if (_treasury == null || GameInstaller.EconomicSell == null)
        {
            sellPreviewText.text = "Sell Preview: -";
            return;
        }

        int gold = GameInstaller.EconomicSell.ComputeSellGold(_treasury);
        sellPreviewText.text = $"Sell Preview: {gold} gold";
    }

    private RowRefs BuildRowRefs(RectTransform row)
    {
        if (row == null)
            return null;

        var key = row.Find("KeyText");
        var value = row.Find("ValueText");

        if (key == null || value == null)
        {
            Debug.LogWarning($"[TreasuryPanelView] Row template '{row.name}' must contain KeyText and ValueText children.");
            return null;
        }

        return new RowRefs
        {
            root = row,
            keyText = key.GetComponent<TMP_Text>(),
            valueText = value.GetComponent<TMP_Text>()
        };
    }

    private void ClearSpawnedRows()
    {
        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (_spawnedRows[i] != null)
                Destroy(_spawnedRows[i].gameObject);
        }

        _spawnedRows.Clear();
    }

    private void OnSellAllClicked()
    {
        if (_treasury == null)
            return;

        _treasury.SellAllToGold();
        RefreshAll();
    }

    private static string Normalize(string id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? string.Empty
            : id.Trim().ToLowerInvariant();
    }
}