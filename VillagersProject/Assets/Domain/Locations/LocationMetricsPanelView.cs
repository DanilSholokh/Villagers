using System.Text;
using TMPro;
using UnityEngine;

public class LocationMetricsPanelView : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;

    private LocationService _locations;
    private SelectedLocationService _selected;

    public void Bind(LocationService locations, SelectedLocationService selected)
    {
        Unbind();

        _locations = locations;
        _selected = selected;

        if (_locations != null)
        {
            _locations.OnLocationChanged += HandleLocationChanged;
            _locations.OnLocationDiscovered += HandleLocationDiscovered;
            _locations.OnLocationWorkersChanged += HandleLocationWorkersChanged;
        }

        if (_selected != null)
            _selected.Changed += HandleSelectedLocationChanged;

        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Unbind()
    {
        if (_locations != null)
        {
            _locations.OnLocationChanged -= HandleLocationChanged;
            _locations.OnLocationDiscovered -= HandleLocationDiscovered;
            _locations.OnLocationWorkersChanged -= HandleLocationWorkersChanged;
        }

        if (_selected != null)
            _selected.Changed -= HandleSelectedLocationChanged;

        _locations = null;
        _selected = null;
    }

    private void HandleSelectedLocationChanged(string _)
    {
        Refresh();
    }

    private void HandleLocationChanged(string locationId)
    {
        if (_selected != null && locationId == _selected.CurrentLocationId)
            Refresh();
    }

    private void HandleLocationDiscovered(string locationId)
    {
        if (_selected != null && locationId == _selected.CurrentLocationId)
            Refresh();
    }

    private void HandleLocationWorkersChanged(string locationId)
    {
        if (_selected != null && locationId == _selected.CurrentLocationId)
            Refresh();
    }

    private void Refresh()
    {
        if (titleText == null || bodyText == null)
            return;

        if (_locations == null || _selected == null || string.IsNullOrWhiteSpace(_selected.CurrentLocationId))
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            titleText.text = "Location";
            bodyText.text = "No location selected.";
            return;
        }

        var loc = _locations.GetLocation(_selected.CurrentLocationId);
        if (loc == null)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            titleText.text = "Location";
            bodyText.text = "Selected location not found.";
            return;
        }

        if (panelRoot != null) panelRoot.SetActive(true);

        titleText.text = string.IsNullOrWhiteSpace(loc.name) ? loc.id : loc.name;
        bodyText.text = BuildBody(loc);
    }

    private string BuildBody(LocationModel loc)
    {
        var sb = new StringBuilder(320);

        int workerCount = loc.currentWorkers != null ? loc.currentWorkers.Count : 0;
        int taskCount = loc.currentTasks != null ? loc.currentTasks.Count : 0;

        sb.AppendLine($"ID: {loc.id}");
        sb.AppendLine($"Status: {loc.status}");
        sb.AppendLine($"Danger: {loc.currentDanger}");
        sb.AppendLine($"Workers: {workerCount}");
        sb.AppendLine($"Active tasks: {taskCount}");
        sb.AppendLine($"Visits: {loc.metrics.totalVisits}");
        sb.AppendLine($"Completed: {loc.metrics.totalTasksCompleted}");
        sb.AppendLine($"Lost: {loc.metrics.villagersLost}");
        sb.AppendLine($"Dead: {loc.metrics.villagersDead}");
        sb.AppendLine($"Gathered: {BuildGatheredResources(loc)}");

        AppendExplorationInfo(sb, loc);

        return sb.ToString().TrimEnd();
    }

    private void AppendExplorationInfo(StringBuilder sb, LocationModel loc)
    {
        if (loc == null || loc.status != LocationStatus.Unknown)
            return;

        var explore = GameInstaller.ExplorationUnlock;

        sb.AppendLine();
        sb.AppendLine("Exploration Unlock:");

        if (explore == null)
        {
            sb.AppendLine("Cost: -");
            sb.AppendLine("Ready: NO");
            sb.AppendLine("Action: publish Explore task from Quest Panel");
            return;
        }

        sb.AppendLine($"Cost: {explore.GetCostText()}");

        bool ready = explore.CanAfford(GameInstaller.Treasury, out _);
        sb.AppendLine($"Ready: {(ready ? "YES" : "NO")}");

        int currentValue = explore.GetCurrentCategoryValue(GameInstaller.Treasury);
        sb.AppendLine($"Current Value: {currentValue}/{explore.RequiredValue}");

        sb.AppendLine("Action: publish Explore task from Quest Panel");
    }

    private string BuildGatheredResources(LocationModel loc)
    {
        if (loc.metrics == null || loc.metrics.resourcesGathered == null || loc.metrics.resourcesGathered.Count == 0)
            return "-";

        var sb = new StringBuilder();
        bool first = true;

        foreach (var kv in loc.metrics.resourcesGathered)
        {
            if (!first)
                sb.Append(", ");

            sb.Append(kv.Key);
            sb.Append(": ");
            sb.Append(kv.Value);

            first = false;
        }

        return sb.ToString();
    }
}