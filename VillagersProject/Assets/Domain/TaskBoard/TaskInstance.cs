using System;

public enum TaskType
{
    Gather = 0,
    ExploreNewLocation = 1,
    SurveyKnownLocation = 2
}

public class TaskInstance
{
    public string taskId;

    // legacy transitional field, keep only for backward compatibility
    public string targetSpotId;

    // source-of-truth for runtime movement/settlement
    public string targetLocationId;

    public TaskType type;
    public string displayName;

    public bool active;
    public int priority;      // 0..5
    public int maxTakers;     // N
    public float durationSec;
    public float baseFailChance = 0.5f; // 0..1

    // legacy reward field
    public int wageGold;

    public float successChance;   // 0..1
    public int riskTier;          // 0..5

    // legacy gather payload
    public string resourceId;
    public int baseAmount;

    // universal payloads
    public ResourceBundle workOutputBundle = new();
    public ResourceBundle taskRewardBundle = new();
    public ResourceBundle taskCostBundle = new();

    public ResourceBundle GetResolvedWorkOutputBundle()
    {
        if (workOutputBundle != null && !workOutputBundle.IsEmpty)
            return workOutputBundle;

        return TaskBundleMapper.BuildLegacyWorkOutputBundle(this);
    }

    public ResourceBundle GetResolvedTaskRewardBundle()
    {
        if (taskRewardBundle != null && !taskRewardBundle.IsEmpty)
            return taskRewardBundle;

        return TaskBundleMapper.BuildLegacyTaskRewardBundle(this);
    }

    public ResourceBundle GetResolvedTaskCostBundle()
    {
        if (taskCostBundle != null && !taskCostBundle.IsEmpty)
            return taskCostBundle;

        return TaskBundleMapper.BuildLegacyTaskCostBundle(this);
    }

    public bool HasBundleModel()
    {
        return (workOutputBundle != null && !workOutputBundle.IsEmpty)
            || (taskRewardBundle != null && !taskRewardBundle.IsEmpty)
            || (taskCostBundle != null && !taskCostBundle.IsEmpty);
    }

    public bool HasUpfrontCost()
    {
        var cost = GetResolvedTaskCostBundle();
        return cost != null && !cost.IsEmpty;
    }

    public bool HasRewardBundle()
    {
        var reward = GetResolvedTaskRewardBundle();
        return reward != null && !reward.IsEmpty;
    }

    public bool UsesLegacyGoldRewardOnly()
    {
        bool hasExplicitRewardBundle = taskRewardBundle != null && !taskRewardBundle.IsEmpty;
        return !hasExplicitRewardBundle && wageGold > 0;
    }

    public bool HasTargetLocation()
    {
        return !string.IsNullOrWhiteSpace(targetLocationId);
    }

    public bool HasPinnedTargetLocation()
    {
        return !string.IsNullOrWhiteSpace(targetLocationId);
    }

    public string GetResolvedTargetLocationId()
    {
        if (!string.IsNullOrWhiteSpace(targetLocationId))
            return targetLocationId;

        return string.Empty;
    }

    public bool UsesLegacySpotTargetOnly()
    {
        return string.IsNullOrWhiteSpace(targetLocationId)
            && !string.IsNullOrWhiteSpace(targetSpotId);
    }

    public void SetTargetLocation(string locationId)
    {
        targetLocationId = NormalizeId(locationId);

        // explicit: legacy field must not be reintroduced as runtime truth
        if (!string.IsNullOrWhiteSpace(targetLocationId))
            targetSpotId = string.Empty;
    }

    public void ClearTargetLocation()
    {
        targetLocationId = string.Empty;
    }

    public void RecalculateDerivedStats()
    {
        successChance = 1f - UnityEngine.Mathf.Clamp01(baseFailChance);

        if (successChance >= 0.85f) riskTier = 0;
        else if (successChance >= 0.7f) riskTier = 1;
        else if (successChance >= 0.55f) riskTier = 2;
        else if (successChance >= 0.4f) riskTier = 3;
        else if (successChance >= 0.25f) riskTier = 4;
        else riskTier = 5;
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}