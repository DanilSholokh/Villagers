using UnityEngine;

public readonly struct GameDebugMessage
{
    public readonly GameDebugChannel Channel;
    public readonly GameDebugSeverity Severity;
    public readonly string Text;
    public readonly Vector3? WorldPosition;
    public readonly string LocationId;
    public readonly string VillagerId;
    public readonly float Lifetime;

    public GameDebugMessage(
        GameDebugChannel channel,
        GameDebugSeverity severity,
        string text,
        Vector3? worldPosition = null,
        string locationId = null,
        string villagerId = null,
        float lifetime = 1.75f)
    {
        Channel = channel;
        Severity = severity;
        Text = text;
        WorldPosition = worldPosition;
        LocationId = locationId;
        VillagerId = villagerId;
        Lifetime = lifetime;
    }
}