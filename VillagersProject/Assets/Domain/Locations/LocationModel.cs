using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LocationModel
{
    public string id;
    public string templateId;
    public string name;

    public LocationStatus status = LocationStatus.Unknown;

    public Vector3 worldPosition;

    public int baseDanger = 0;
    public int currentDanger = 0;

    public List<LocationResource> resources = new();
    public List<LocationResource> potentialResources = new();
    public LocationMetrics metrics = new();

    public List<string> currentWorkers = new();
    public List<string> currentTasks = new();

    public bool IsBlocked => status == LocationStatus.Blocked;
}