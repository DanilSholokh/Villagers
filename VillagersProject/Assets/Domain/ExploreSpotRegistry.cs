using System.Collections.Generic;
using UnityEngine;

public class ExploreSpotRegistry
{
    public List<ExploreSpotAuthoring> Spots { get; private set; }

    public void Initialize()
    {
        Spots = new List<ExploreSpotAuthoring>(
            Object.FindObjectsByType<ExploreSpotAuthoring>(FindObjectsSortMode.None)
        );

        Debug.Log($"[ExploreSpotRegistry] Found {Spots.Count} spots");

        foreach (var s in Spots)
        {
            Debug.Log($" - {s.spotId} weight={s.weight} pos={s.transform.position}");
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
}




