using System.Collections.Generic;
using UnityEngine;

public class ExploreSpotRegistry
{
    public List<ExploreSpotAuthoring> Spots { get; private set; }

    public Dictionary<string, LocationEntity> Locations { get; private set; }
    public Dictionary<string, ExploreSpotAuthoring> AuthoringsById { get; private set; }

    public void Initialize()
    {
        Spots = new List<ExploreSpotAuthoring>(
            Object.FindObjectsByType<ExploreSpotAuthoring>(FindObjectsSortMode.None)
        );



        Locations = new Dictionary<string, LocationEntity>();
        AuthoringsById = new Dictionary<string, ExploreSpotAuthoring>();

        foreach (var s in Spots)
        {
            if (s == null) continue;

            if (string.IsNullOrWhiteSpace(s.spotId))
            {
                Debug.LogWarning($"[ExploreSpotRegistry] Spot has empty spotId on object={s.name}");
                continue;
            }

            if (AuthoringsById.ContainsKey(s.spotId))
            {
                Debug.LogError($"[ExploreSpotRegistry] Duplicate spotId={s.spotId}. Object={s.name}");
                continue;
            }

            AuthoringsById[s.spotId] = s;

            var loc = new LocationEntity(
                spotId: s.spotId,
                displayName: string.IsNullOrWhiteSpace(s.displayName) ? s.spotId : s.displayName,
                gatherResourceId: s.gatherResourceId,
                position: s.transform.position,
                weight: s.weight
            );

            Locations[s.spotId] = loc;
        }

    }


    public ExploreSpotAuthoring GetRandomSpotWeighted()
    {
        if (Spots == null || Spots.Count == 0)
            return null;

        float total = 0f;
        for (int i = 0; i < Spots.Count; i++)
        {
            float w = Mathf.Max(0f, Spots[i].weight);
            total += w;
        }

        // якщо вс≥ ваги 0 Ч fallback: просто random будь-€кий
        if (total <= 0.0001f)
            return Spots[Random.Range(0, Spots.Count)];

        float roll = Random.Range(0f, total);
        float acc = 0f;

        for (int i = 0; i < Spots.Count; i++)
        {
            acc += Mathf.Max(0f, Spots[i].weight);
            if (roll <= acc)
                return Spots[i];
        }

        return Spots[Spots.Count - 1];
    }



    public ExploreSpotAuthoring PickGatherSpotWeighted(string resourceId)
    {
        if (Spots == null || Spots.Count == 0) return null;
        if (string.IsNullOrWhiteSpace(resourceId)) return null;

        // в≥дф≥льтрувати споти, де Ї цей ресурс
        var candidates = new List<ExploreSpotAuthoring>();
        for (int i = 0; i < Spots.Count; i++)
        {
            var s = Spots[i];
            if (s == null) continue;
            if (string.Equals(s.gatherResourceId, resourceId, System.StringComparison.OrdinalIgnoreCase))
                candidates.Add(s);
        }

        if (candidates.Count == 0) return null;

        // weighted pick по weight
        float total = 0f;
        for (int i = 0; i < candidates.Count; i++)
            total += Mathf.Max(0f, candidates[i].weight);

        if (total <= 0.0001f)
            return candidates[Random.Range(0, candidates.Count)];

        float roll = Random.Range(0f, total);
        float acc = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            acc += Mathf.Max(0f, candidates[i].weight);
            if (roll <= acc) return candidates[i];
        }

        return candidates[candidates.Count - 1];
    }


    public ExploreSpotAuthoring GetSpotById(string spotId)
    {
        if (string.IsNullOrWhiteSpace(spotId)) return null;
        return AuthoringsById != null && AuthoringsById.TryGetValue(spotId, out var s) ? s : null;
    }


    public int GetDangerTier(string spotId)
    {
        if (Spots == null) return 0;
        var s = Spots.Find(x => x != null && x.spotId == spotId);
        return (s != null) ? Mathf.Clamp(s.dangerTier, 0, 5) : 0;
    }




}






