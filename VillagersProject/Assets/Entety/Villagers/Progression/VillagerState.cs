using System;

public enum VillagerStatus
{
    Idle = 0,
    LookingForTask = 1,
    ReservedTask = 2,
    MovingToTarget = 3,
    Working = 4,
    ReturningHome = 5,
}

public class VillagerState
{
    public string agentId;
    public string displayName;

    public VillagerStatus status;
    public string taskId;
    public string taskName;

    public DateTime lastChangeUtc;
}