using System;
using System.Collections.Generic;
using UnityEngine;

public class LocationService
{

    private readonly LocationRegistry _registry;

    public event Action<string> OnLocationChanged;
    public event Action<string> OnLocationDiscovered;
    public event Action<string> OnLocationWorkersChanged;



    public LocationService(LocationRegistry registry)
    {
        _registry = registry;
    }

    public LocationModel GetLocation(string locationId)
        => _registry.Get(locationId);

    public IReadOnlyCollection<LocationModel> GetAllLocations()
        => _registry.GetAll();

    public bool IsDiscovered(string locationId)
    {
        var loc = _registry.Get(locationId);
        return loc != null && loc.status == LocationStatus.Discovered;
    }

    public bool HasAnyDiscoveredLocationForResource(string resourceId)
    {
        return !string.IsNullOrWhiteSpace(
            FindRandomLocationForResource(resourceId, onlyDiscovered: true, onlyUnlocked: true)
        );
    }

    public string FindAnyDiscoveredLocationForResource(string resourceId)
    {
        // legacy name kept for compatibility, but selection path is random-from-valid-pool
        return FindRandomLocationForResource(resourceId, onlyDiscovered: true, onlyUnlocked: true);
    }

    public string FindRandomUndiscoveredLocationId()
    {
        var list = _registry.FindUndiscovered();
        if (list.Count == 0) return null;
        return list[UnityEngine.Random.Range(0, list.Count)].id;
    }

    public Vector3 GetWorldPosition(string locationId)
    {
        var loc = _registry.Get(locationId);
        return loc != null ? loc.worldPosition : Vector3.zero;
    }

    public int GetDanger(string locationId)
    {
        var loc = _registry.Get(locationId);
        return loc != null ? Mathf.Clamp(loc.currentDanger, 0, 5) : 0;
    }

    public void DiscoverLocation(string locationId)
    {
        var loc = _registry.Get(locationId);
        if (loc == null) return;

        bool changed = false;
        var unlockedNow = new List<string>();

        if (loc.status != LocationStatus.Discovered)
        {
            loc.status = LocationStatus.Discovered;
            changed = true;
        }

        if (loc.resources != null)
        {
            for (int i = 0; i < loc.resources.Count; i++)
            {
                var r = loc.resources[i];
                if (r == null) continue;

                if (!r.isUnlocked)
                {
                    r.isUnlocked = true;
                    unlockedNow.Add(r.resourceId);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            Dbg($"DiscoverLocation id={locationId} name={loc.name} unlocked=[{(unlockedNow.Count > 0 ? string.Join(", ", unlockedNow) : "(none)")}]");
            RaiseChanged(locationId);
            OnLocationDiscovered?.Invoke(locationId);
        }
    }

    public void RegisterWorker(string locationId, string villagerId, string taskId = null)
    {
        var loc = _registry.Get(locationId);
        if (loc == null || string.IsNullOrWhiteSpace(villagerId)) return;

        if (!loc.currentWorkers.Contains(villagerId))
            loc.currentWorkers.Add(villagerId);

        if (!string.IsNullOrWhiteSpace(taskId) && !loc.currentTasks.Contains(taskId))
            loc.currentTasks.Add(taskId);

        Dbg($"RegisterWorker location={locationId} worker={villagerId} task={taskId ?? "(none)"} workers=[{FormatStringList(loc.currentWorkers)}]");

        RaiseChanged(locationId);
        OnLocationWorkersChanged?.Invoke(locationId);
    }

    public void RemoveWorker(string locationId, string villagerId, string taskId = null)
    {
        var loc = _registry.Get(locationId);
        if (loc == null || string.IsNullOrWhiteSpace(villagerId)) return;

        loc.currentWorkers.Remove(villagerId);

        if (!string.IsNullOrWhiteSpace(taskId))
            loc.currentTasks.Remove(taskId);

        Dbg($"RemoveWorker location={locationId} worker={villagerId} task={taskId ?? "(none)"} workers=[{FormatStringList(loc.currentWorkers)}]");

        RaiseChanged(locationId);
        OnLocationWorkersChanged?.Invoke(locationId);
    }

    public void AddVisit(string locationId)
    {
        var loc = _registry.Get(locationId);
        if (loc == null) return;

        loc.metrics.totalVisits++;
        Dbg($"AddVisit location={locationId} visits={loc.metrics.totalVisits}");
        RaiseChanged(locationId);
    }

    public void AddResourceGathered(string locationId, string resourceId, int amount)
    {
        var loc = _registry.Get(locationId);
        if (loc == null || string.IsNullOrWhiteSpace(resourceId) || amount <= 0) return;

        if (!loc.metrics.resourcesGathered.ContainsKey(resourceId))
            loc.metrics.resourcesGathered[resourceId] = 0;

        loc.metrics.resourcesGathered[resourceId] += amount;

        Dbg($"AddResourceGathered location={locationId} resource={resourceId} amount=+{amount} total={loc.metrics.resourcesGathered[resourceId]}");

        RaiseChanged(locationId);
    }

    public void AddTaskCompleted(string locationId)
    {
        var loc = _registry.Get(locationId);
        if (loc == null) return;

        loc.metrics.totalTasksCompleted++;
        Dbg($"AddTaskCompleted location={locationId} totalTasksCompleted={loc.metrics.totalTasksCompleted}");
        RaiseChanged(locationId);
    }

    public void AddVillagerLost(string locationId)
    {
        var loc = _registry.Get(locationId);
        if (loc == null) return;

        loc.metrics.villagersLost++;
        Dbg($"AddVillagerLost location={locationId} villagersLost={loc.metrics.villagersLost}");
        RaiseChanged(locationId);
    }

    public void AddVillagerDead(string locationId)
    {
        var loc = _registry.Get(locationId);
        if (loc == null) return;

        loc.metrics.villagersDead++;
        Dbg($"AddVillagerDead location={locationId} villagersDead={loc.metrics.villagersDead}");
        RaiseChanged(locationId);
    }

    private void RaiseChanged(string locationId)
    {
        OnLocationChanged?.Invoke(locationId);
    }

    public bool HasUnlockedResource(string locationId, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(resourceId))
            return false;

        var loc = _registry.Get(locationId);
        if (loc == null || loc.resources == null)
            return false;

        resourceId = resourceId.ToLowerInvariant();

        for (int i = 0; i < loc.resources.Count; i++)
        {
            var r = loc.resources[i];
            if (r == null) continue;

            if (string.Equals(r.resourceId, resourceId, StringComparison.OrdinalIgnoreCase) && r.isUnlocked)
                return true;
        }

        return false;
    }

    public bool HasResource(string locationId, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(resourceId))
            return false;

        var loc = _registry.Get(locationId);
        if (loc == null || loc.resources == null)
            return false;

        resourceId = resourceId.ToLowerInvariant();

        for (int i = 0; i < loc.resources.Count; i++)
        {
            var r = loc.resources[i];
            if (r == null)
                continue;

            if (string.Equals(r.resourceId, resourceId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public List<LocationResource> GetUnlockedResources(string locationId)
    {
        var result = new List<LocationResource>();

        if (string.IsNullOrWhiteSpace(locationId))
            return result;

        var loc = _registry.Get(locationId);
        if (loc == null || loc.resources == null)
            return result;

        for (int i = 0; i < loc.resources.Count; i++)
        {
            var r = loc.resources[i];
            if (r == null) continue;
            if (!r.isUnlocked) continue;

            result.Add(r);
        }

        return result;
    }

    public string FindAnyLocationForResource(string resourceId, bool onlyDiscovered = true, bool onlyUnlocked = true)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            return null;

        var found = _registry.FindByResource(resourceId, onlyUnlocked, onlyDiscovered);
        return found.Count > 0 ? found[0].id : null;
    }

    public void UnlockResource(string locationId, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(resourceId))
            return;

        var loc = _registry.Get(locationId);
        if (loc == null || loc.resources == null)
            return;

        resourceId = resourceId.ToLowerInvariant();

        for (int i = 0; i < loc.resources.Count; i++)
        {
            var r = loc.resources[i];
            if (r == null) continue;

            if (string.Equals(r.resourceId, resourceId, StringComparison.OrdinalIgnoreCase))
            {
                if (!r.isUnlocked)
                {
                    r.isUnlocked = true;
                    RaiseChanged(locationId);
                }
                return;
            }
        }
    }

    public void AddResourceIfMissing(
        string locationId,
        string resourceId,
        bool unlocked = true,
        float timePerUnit = 1f,
        int difficulty = 0)
    {
        if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(resourceId))
            return;

        var loc = _registry.Get(locationId);
        if (loc == null)
            return;

        if (loc.resources == null)
            loc.resources = new List<LocationResource>();

        resourceId = resourceId.ToLowerInvariant();

        for (int i = 0; i < loc.resources.Count; i++)
        {
            var r = loc.resources[i];
            if (r == null) continue;

            if (string.Equals(r.resourceId, resourceId, StringComparison.OrdinalIgnoreCase))
                return;
        }

        loc.resources.Add(new LocationResource
        {
            resourceId = resourceId,
            isUnlocked = unlocked,
            timePerUnit = Mathf.Max(0.01f, timePerUnit),
            difficulty = Mathf.Clamp(difficulty, 0, 5)
        });

        RaiseChanged(locationId);
    }



    public bool HasPotentialResource(string locationId, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(resourceId))
            return false;

        var loc = _registry.Get(locationId);
        if (loc == null || loc.potentialResources == null)
            return false;

        resourceId = resourceId.ToLowerInvariant();

        for (int i = 0; i < loc.potentialResources.Count; i++)
        {
            var r = loc.potentialResources[i];
            if (r == null) continue;

            if (string.Equals(r.resourceId, resourceId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public List<LocationResource> GetPotentialResources(string locationId)
    {
        var result = new List<LocationResource>();

        if (string.IsNullOrWhiteSpace(locationId))
            return result;

        var loc = _registry.Get(locationId);
        if (loc == null || loc.potentialResources == null)
            return result;

        for (int i = 0; i < loc.potentialResources.Count; i++)
        {
            var r = loc.potentialResources[i];
            if (r == null) continue;

            result.Add(r);
        }

        return result;
    }

    public bool RevealPotentialResource(string locationId, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(resourceId))
            return false;

        var loc = _registry.Get(locationId);
        if (loc == null || loc.potentialResources == null)
            return false;

        resourceId = resourceId.ToLowerInvariant();

        for (int i = 0; i < loc.potentialResources.Count; i++)
        {
            var r = loc.potentialResources[i];
            if (r == null) continue;

            if (!string.Equals(r.resourceId, resourceId, StringComparison.OrdinalIgnoreCase))
                continue;

            loc.potentialResources.RemoveAt(i);

            AddResourceIfMissing(
                locationId,
                resourceId,
                unlocked: true,
                timePerUnit: r.timePerUnit,
                difficulty: r.difficulty
            );

            Dbg($"RevealPotentialResource location={locationId} name={loc.name} resource={resourceId}");

            RaiseChanged(locationId);
            return true;
        }

        return false;
    }

    public bool TryRevealRandomPotentialResource(string locationId)
    {
        if (string.IsNullOrWhiteSpace(locationId))
            return false;

        var loc = _registry.Get(locationId);
        if (loc == null || loc.potentialResources == null || loc.potentialResources.Count == 0)
        {
            Dbg($"TryRevealRandomPotentialResource location={locationId} -> no potential resources");
            return false;
        }

        int index = UnityEngine.Random.Range(0, loc.potentialResources.Count);
        var r = loc.potentialResources[index];
        if (r == null) return false;

        Dbg($"TryRevealRandomPotentialResource location={locationId} picked={r.resourceId}");
        return RevealPotentialResource(locationId, r.resourceId);
    }

    public bool TryRevealRandomPotentialResourceDetailed(string locationId, out string revealedResourceId)
    {
        revealedResourceId = string.Empty;

        if (string.IsNullOrWhiteSpace(locationId))
            return false;

        var loc = _registry.Get(locationId);
        if (loc == null || loc.potentialResources == null || loc.potentialResources.Count == 0)
        {
            Dbg($"TryRevealRandomPotentialResourceDetailed location={locationId} -> no potential resources");
            return false;
        }

        int index = UnityEngine.Random.Range(0, loc.potentialResources.Count);
        var r = loc.potentialResources[index];
        if (r == null || string.IsNullOrWhiteSpace(r.resourceId))
            return false;

        revealedResourceId = r.resourceId;
        Dbg($"TryRevealRandomPotentialResourceDetailed location={locationId} picked={revealedResourceId}");

        return RevealPotentialResource(locationId, revealedResourceId);
    }

    public IReadOnlyCollection<string> GetDiscoveredLocationIds()
    {
        var result = new List<string>();

        foreach (var loc in _registry.GetAll())
        {
            if (loc != null && loc.status == LocationStatus.Discovered)
                result.Add(loc.id);
        }

        return result;
    }


    public List<string> FindLocationsForResource(
     string resourceId,
     bool onlyDiscovered,
     bool onlyUnlocked)
    {
        var result = new List<string>();

        resourceId = Normalize(resourceId);
        if (string.IsNullOrWhiteSpace(resourceId))
            return result;

        var all = GetAllLocations();
        if (all == null)
            return result;

        foreach (var loc in all)
        {
            if (loc == null || string.IsNullOrWhiteSpace(loc.id))
                continue;

            if (onlyDiscovered && loc.status != LocationStatus.Discovered)
                continue;

            if (loc.resources == null || loc.resources.Count == 0)
                continue;

            bool hasResource = false;

            for (int i = 0; i < loc.resources.Count; i++)
            {
                var r = loc.resources[i];
                if (r == null)
                    continue;

                if (!string.Equals(r.resourceId, resourceId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (onlyUnlocked && !r.isUnlocked)
                    continue;

                hasResource = true;
                break;
            }

            if (hasResource)
                result.Add(loc.id);
        }

        return result;
    }

    public string FindRandomLocationForResource(
        string resourceId,
        bool onlyDiscovered,
        bool onlyUnlocked)
    {
        var candidates = FindLocationsForResource(resourceId, onlyDiscovered, onlyUnlocked);
        if (candidates == null || candidates.Count == 0)
            return string.Empty;

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private void Dbg(string text)
    {
        GameDebug.Info(GameDebugChannel.Location, text);
    }

    private static string FormatResourceList(List<LocationResource> resources)
    {
        if (resources == null || resources.Count == 0)
            return "(none)";

        var parts = new List<string>();

        for (int i = 0; i < resources.Count; i++)
        {
            var r = resources[i];
            if (r == null) continue;

            parts.Add($"{r.resourceId}[unlocked={r.isUnlocked}, t={r.timePerUnit:0.##}, d={r.difficulty}]");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "(none)";
    }

    private static string FormatStringList(List<string> items)
    {
        if (items == null || items.Count == 0)
            return "(none)";

        return string.Join(", ", items);
    }


    public string FindRandomUnknownLocationId()
    {
        var list = _registry.FindUndiscovered();
        if (list == null || list.Count == 0)
            return null;

        return list[UnityEngine.Random.Range(0, list.Count)].id;
    }

    public string FindRandomDiscoveredLocationWithPotentialResource()
    {
        var list = _registry.FindDiscoveredWithPotentialResources();
        if (list == null || list.Count == 0)
            return null;

        return list[UnityEngine.Random.Range(0, list.Count)].id;
    }

    public string FindRandomDiscoveredLocationId()
    {
        var list = _registry.FindDiscovered();
        if (list == null || list.Count == 0)
            return null;

        return list[UnityEngine.Random.Range(0, list.Count)].id;
    }

    public bool HasAnyDiscoveredLocation()
    {
        foreach (var loc in _registry.GetAll())
        {
            if (loc != null && loc.status == LocationStatus.Discovered)
                return true;
        }

        return false;
    }

    [ContextMenu("Dump Locations")]
    public void DumpAllLocationsToLog()
    {
        if (!GameDebug.EnableLocationChannel)
            return;

        if (_registry == null)
        {
            GameDebug.Warning(
                GameDebugChannel.Location,
                "DumpAllLocationsToLog skipped: registry is null"
            );
            return;
        }

        GameDebug.Info(GameDebugChannel.Location, "=== Locations Dump Start ===");

        foreach (var loc in _registry.GetAll())
        {
            if (loc == null)
            {
                GameDebug.Warning(GameDebugChannel.Location, "Null location entry");
                continue;
            }

            int workerCount = loc.currentWorkers != null ? loc.currentWorkers.Count : 0;

            GameDebug.Info(
                GameDebugChannel.Location,
                $"Location '{loc.id}' | workers={workerCount}"
            );
        }

        GameDebug.Info(GameDebugChannel.Location, "=== Locations Dump End ===");
    }


    


}