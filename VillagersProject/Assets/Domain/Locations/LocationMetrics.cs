using System;
using System.Collections.Generic;

[Serializable]
public class LocationMetrics
{
    public Dictionary<string, int> resourcesGathered = new();
    public int villagersLost = 0;
    public int villagersDead = 0;
    public int totalTasksCompleted = 0;
    public int totalVisits = 0;
}