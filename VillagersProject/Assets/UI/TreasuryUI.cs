using TMPro;
using UnityEngine;

public class TreasuryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    private void Update()
    {
        if (text == null || GameInstaller.Treasury == null) return;

        // Поки вручну 3 ресурси (MVP)
        int wood = GameInstaller.Treasury.GetAmount("wood");
        int fish = GameInstaller.Treasury.GetAmount("fish");
        int stone = GameInstaller.Treasury.GetAmount("stone");

        text.text = $"WOOD: {wood}\nFISH: {fish}\nSTONE: {stone}";
    }
}
