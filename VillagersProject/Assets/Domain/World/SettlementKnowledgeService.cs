using System;
using System.Collections.Generic;

public class SettlementKnowledgeService
{

    public event Action OnChanged;
    public IReadOnlyCollection<string> DiscoveredIds
    => GameInstaller.LocationService != null
        ? GameInstaller.LocationService.GetDiscoveredLocationIds()
        : Array.Empty<string>();

    public bool IsDiscovered(string spotId)
    {
        return GameInstaller.LocationService != null &&
               GameInstaller.LocationService.IsDiscovered(spotId);
    }

    public void Discover(string spotId)
    {
        if (string.IsNullOrWhiteSpace(spotId)) return;
        if (GameInstaller.LocationService == null) return;

        bool wasDiscovered = GameInstaller.LocationService.IsDiscovered(spotId);
        GameInstaller.LocationService.DiscoverLocation(spotId);

        if (!wasDiscovered && GameInstaller.LocationService.IsDiscovered(spotId))
            OnChanged?.Invoke();
    }

    

}