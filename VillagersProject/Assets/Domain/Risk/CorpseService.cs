using System.Collections.Generic;
using UnityEngine;

public class CorpseRecord
{
    public string agentId;
    public Vector3 worldPos;
    public int escrowGold;
    public Dictionary<string, int> cargo;
}

public class CorpseService
{
    public readonly List<CorpseRecord> Records = new();

    public void AddCorpse(CorpseRecord r)
    {
        Records.Add(r);
        SpawnMarker(r.worldPos, "⚰");
    }

    private void SpawnMarker(Vector3 pos, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = $"Marker_Corpse_{name}";
        go.transform.position = pos + Vector3.up * 1.2f;
        go.transform.localScale = new Vector3(0.6f, 0.2f, 0.6f);
    }
}