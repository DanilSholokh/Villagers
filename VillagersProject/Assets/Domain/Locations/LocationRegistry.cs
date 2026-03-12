using System;
using System.Collections.Generic;
using System.Linq;

public class LocationRegistry
{
    private readonly Dictionary<string, LocationModel> _locations = new();

    public void Clear()
    {
        _locations.Clear();
    }

    public void Add(LocationModel location)
    {
        if (location == null) return;
        if (string.IsNullOrWhiteSpace(location.id)) return;

        _locations[location.id] = location;
    }

    public bool TryGet(string id, out LocationModel location)
    {
        return _locations.TryGetValue(id, out location);
    }

    public LocationModel Get(string id)
    {
        return _locations.TryGetValue(id, out var loc) ? loc : null;
    }

    public IReadOnlyCollection<LocationModel> GetAll()
    {
        return _locations.Values;
    }

    public List<LocationModel> FindByResource(string resourceId, bool onlyUnlocked = true, bool onlyDiscovered = false)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            return new List<LocationModel>();

        resourceId = resourceId.ToLowerInvariant();

        return _locations.Values
            .Where(loc =>
                loc != null &&
                (!onlyDiscovered || loc.status == LocationStatus.Discovered) &&
                loc.resources != null &&
                loc.resources.Exists(r =>
                    r != null &&
                    string.Equals(r.resourceId, resourceId, StringComparison.OrdinalIgnoreCase) &&
                    (!onlyUnlocked || r.isUnlocked)))
            .ToList();
    }

    public List<LocationModel> FindUndiscovered()
    {
        return _locations.Values
            .Where(x => x.status == LocationStatus.Unknown)
            .ToList();
    }


    public List<LocationModel> FindDiscovered()
    {
        return _locations.Values
            .Where(x => x != null && x.status == LocationStatus.Discovered)
            .ToList();
    }

    public List<LocationModel> FindDiscoveredWithPotentialResources()
    {
        return _locations.Values
            .Where(x =>
                x != null &&
                x.status == LocationStatus.Discovered &&
                x.potentialResources != null &&
                x.potentialResources.Count > 0)
            .ToList();
    }


}