using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TreasuryPanelView : MonoBehaviour
{
    [Header("Values")]
    public TextMeshProUGUI woodValue;
    public TextMeshProUGUI stoneValue;
    public TextMeshProUGUI fishValue;
    public TextMeshProUGUI goldValue;


    [Header("Buttons")]
    public Button sellAllButton;


    private TreasuryService _treasury;


    // v0.01 фіксовані ціни (MVP)
    private static readonly Dictionary<string, int> PRICE = new()
    {
        { "wood", 1 },
        { "stone", 2 },
        { "fish", 3 }
    };


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

        RefreshAll();
    }

    private void OnEnable()
    {
        // якщо панель вимикали/вмикали — треба перепідписатись назад
        if (_treasury != null)
        {
            _treasury.OnChanged -= HandleChanged; // safety від дублю
            _treasury.OnChanged += HandleChanged;
        }

        if (sellAllButton != null)
        {
            sellAllButton.onClick.RemoveListener(OnSellAllClicked);
            sellAllButton.onClick.AddListener(OnSellAllClicked);
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (_treasury != null)
            _treasury.OnChanged -= HandleChanged;

        if (sellAllButton != null)
            sellAllButton.onClick.RemoveListener(OnSellAllClicked);
    }

    private void HandleChanged(string resId, int newTotal)
    {
        resId = (resId ?? "").ToLowerInvariant();

        switch (resId)
        {
            case "wood": if (woodValue) woodValue.text = newTotal.ToString(); break;
            case "stone": if (stoneValue) stoneValue.text = newTotal.ToString(); break;
            case "fish": if (fishValue) fishValue.text = newTotal.ToString(); break;
            case "gold":
                if (goldValue) goldValue.text = _treasury.GetGoldUi();
                break;
        }
    }

    private void RefreshAll()
    {
        if (_treasury == null) return;

        if (woodValue) woodValue.text = _treasury.GetAmount("wood").ToString();
        if (stoneValue) stoneValue.text = _treasury.GetAmount("stone").ToString();
        if (fishValue) fishValue.text = _treasury.GetAmount("fish").ToString();
        if (goldValue) goldValue.text = _treasury.GetGoldUi();
    }

    private void OnSellAllClicked()
    {
        if (_treasury == null) return;

        _treasury.SellAllToGold(PRICE);
        RefreshAll();
    }



}
