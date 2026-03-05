using System.Collections.Generic;
using UnityEngine;

public class LostRecord
{
    public string agentId;
    public Vector3 lastSeenPos;
    public int escrowGold;
    public Dictionary<string, int> cargo;
    public LostHiddenOutcome hiddenOutcome;
}

public class LostService
{
    public readonly List<LostRecord> Records = new();

    public void AddLost(LostRecord r)
    {
        Records.Add(r);
        SpawnMarker(r.lastSeenPos, "?");
    }

    private void SpawnMarker(Vector3 pos, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = $"Marker_Lost_{name}";
        go.transform.position = pos + Vector3.up * 1.2f;
        go.transform.localScale = Vector3.one * 0.5f;
    }

}