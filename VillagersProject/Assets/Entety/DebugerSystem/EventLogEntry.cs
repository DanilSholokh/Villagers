using System;

public readonly struct EventLogEntry
{
    public readonly DateTime TimeUtc;
    public readonly GameDebugChannel Channel;
    public readonly GameDebugSeverity Severity;
    public readonly string Text;
    public readonly string VillagerId;
    public readonly string LocationId;

    public EventLogEntry(
        DateTime timeUtc,
        GameDebugChannel channel,
        GameDebugSeverity severity,
        string text,
        string villagerId = null,
        string locationId = null)
    {
        TimeUtc = timeUtc;
        Channel = channel;
        Severity = severity;
        Text = text;
        VillagerId = villagerId;
        LocationId = locationId;
    }
}