using System;
using System.Collections.Generic;

public enum TaskType
{
    Gather = 0,
    Explore = 1
}

public class TaskInstance
{
    public string taskId;
    public TaskType type;
    public string displayName;

    public bool active;
    public int priority;      // 0..5
    public int maxTakers;     // N
    public float durationSec;

    // Для Gather (поки опціонально)
    public string resourceId;
    public int baseAmount;

    // Для Explore: використовуємо ExploreSpotRegistry + OutcomeService
}

