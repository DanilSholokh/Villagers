using TMPro;
using UnityEngine;

public class TreasuryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    private void Update()
    {
        if (text == null)
            return;

        var treasury = GameInstaller.Treasury;
        if (treasury == null)
        {
            text.text = "Treasury: -";
            return;
        }

        var snapshot = treasury.ReadOnlySnapshot();
        if (snapshot == null || snapshot.Count == 0)
        {
            text.text = "Treasury: empty";
            return;
        }

        var lines = new System.Text.StringBuilder(256);
        lines.AppendLine("Treasury");

        foreach (var resource in GameInstaller.Resources != null ? GameInstaller.Resources.GetAll() : null)
        {
            if (resource == null)
                continue;

            string resourceId = Normalize(resource.resourceId);
            if (string.IsNullOrWhiteSpace(resourceId))
                continue;

            int amount = treasury.GetAmount(resourceId);
            if (amount <= 0)
                continue;

            string displayName = string.IsNullOrWhiteSpace(resource.displayName)
                ? resourceId
                : resource.displayName;

            lines.AppendLine($"{displayName}: {amount}");
        }

        text.text = lines.ToString().TrimEnd();
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}