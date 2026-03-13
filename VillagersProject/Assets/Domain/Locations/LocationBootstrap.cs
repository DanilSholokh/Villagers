using System.Collections.Generic;
using UnityEngine;

public static class LocationBootstrap
{
    public static void BuildFromScene(LocationRegistry registry)
    {
        if (registry == null) return;

        registry.Clear();

        var spots = Object.FindObjectsByType<ExploreSpotAuthoring>(FindObjectsSortMode.None);

        foreach (var spot in spots)
        {
            if (spot == null) continue;
            if (string.IsNullOrWhiteSpace(spot.spotId)) continue;

            var model = new LocationModel
            {
                id = spot.spotId,
                templateId = spot.spotId,
                name = string.IsNullOrWhiteSpace(spot.displayName) ? spot.spotId : spot.displayName,
                status = LocationStatus.Unknown,
                worldPosition = spot.transform.position,
                baseDanger = Mathf.Clamp(spot.dangerTier, 0, 5),
                currentDanger = Mathf.Clamp(spot.dangerTier, 0, 5)
            };

            if (!string.IsNullOrWhiteSpace(spot.gatherResourceId))
            {
                model.resources.Add(new LocationResource
                {
                    resourceId = spot.gatherResourceId.ToLowerInvariant(),
                    timePerUnit = 1f,
                    difficulty = Mathf.Clamp(spot.dangerTier, 0, 5),
                    isUnlocked = false
                });
            }

            if (spot.potentialResourceIds != null)
            {
                for (int i = 0; i < spot.potentialResourceIds.Length; i++)
                {
                    var resId = spot.potentialResourceIds[i];
                    if (string.IsNullOrWhiteSpace(resId))
                        continue;

                    resId = resId.ToLowerInvariant();

                    bool alreadyInBase = false;
                    for (int j = 0; j < model.resources.Count; j++)
                    {
                        var baseRes = model.resources[j];
                        if (baseRes == null) continue;

                        if (string.Equals(baseRes.resourceId, resId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyInBase = true;
                            break;
                        }
                    }

                    if (alreadyInBase)
                        continue;

                    bool alreadyInPotential = false;
                    for (int j = 0; j < model.potentialResources.Count; j++)
                    {
                        var potentialRes = model.potentialResources[j];
                        if (potentialRes == null) continue;

                        if (string.Equals(potentialRes.resourceId, resId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyInPotential = true;
                            break;
                        }
                    }

                    if (alreadyInPotential)
                        continue;

                    model.potentialResources.Add(new LocationResource
                    {
                        resourceId = resId,
                        timePerUnit = 1f,
                        difficulty = Mathf.Clamp(spot.dangerTier, 0, 5),
                        isUnlocked = false
                    });
                }
            }


            registry.Add(model);

        }
    }

    private static string JoinResourceIds(List<LocationResource> resources)
    {
        if (resources == null || resources.Count == 0)
            return "(none)";

        var parts = new List<string>();

        for (int i = 0; i < resources.Count; i++)
        {
            var r = resources[i];
            if (r == null) continue;
            parts.Add(r.resourceId);
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "(none)";
    }

}