using System;
using System.Collections.Generic;

public enum TaskType
{
    Gather = 0,
    ExploreNewLocation = 1,
    SurveyKnownLocation = 2
}

public class TaskInstance
{
    public string taskId;
    public string targetSpotId; // legacy transitional field, do not use in new logic
    public string targetLocationId; // new source-of-truth

    public TaskType type;
    public string displayName;

    public bool active;
    public int priority;      // 0..5
    public int maxTakers;     // N
    public float durationSec;
    public float baseFailChance = 0.5f; // 0..1

    public int wageGold;

    public float successChance;   // 0..1 (показується в UI)
    public int riskTier;          // 0..5 (тільки індикатор)

    // Для Gather (поки опціонально)
    public string resourceId;
    public int baseAmount;

    // Для Explore: використовуємо ExploreSpotRegistry + OutcomeService

}

