using TMPro;
using UnityEngine;

public class TreasuryPanelView : MonoBehaviour
{
    [Header("Values")]
    public TextMeshProUGUI woodValue;
    public TextMeshProUGUI stoneValue;
    public TextMeshProUGUI fishValue;

    private TreasuryService _treasury;

    public void Bind(TreasuryService treasury)
    {
        if (_treasury != null)
            _treasury.OnChanged -= HandleChanged;

        _treasury = treasury;

        if (_treasury != null)
            _treasury.OnChanged += HandleChanged;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (_treasury != null)
            _treasury.OnChanged -= HandleChanged;
    }

    private void HandleChanged(string resId, int newTotal)
    {
        switch (resId)
        {
            case "wood": if (woodValue) woodValue.text = newTotal.ToString(); break;
            case "stone": if (stoneValue) stoneValue.text = newTotal.ToString(); break;
            case "fish": if (fishValue) fishValue.text = newTotal.ToString(); break;
        }
    }

    private void RefreshAll()
    {
        if (_treasury == null) return;
        if (woodValue) woodValue.text = _treasury.Get("wood").ToString();
        if (stoneValue) stoneValue.text = _treasury.Get("stone").ToString();
        if (fishValue) fishValue.text = _treasury.Get("fish").ToString();
    }
}
