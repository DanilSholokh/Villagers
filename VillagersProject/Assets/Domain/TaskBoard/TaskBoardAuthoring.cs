using System;
using System.Collections.Generic;
using UnityEngine;

public class TaskBoardAuthoring : MonoBehaviour
{
    [Serializable]
    public class TaskDef
    {
        public string taskId;
        public TaskType type;
        public string displayName;

        public bool active = true;

        [Range(0f, 1f)] public float baseFailChance = 0f;
        [Range(0, 5)] public int priority = 3;
        [Min(1)] public int maxTakers = 1;
        [Min(0.1f)] public float durationSec = 10f;

        [Min(0)] public int wageGold = 5;

        // PATCH 11: optional exact reward override
        public string rewardResourceId;
        [Min(0)] public int rewardAmount = 0;

        // PATCH 11: optional exact upfront cost
        public string upfrontCostResourceId;
        [Min(0)] public int upfrontCostAmount = 0;

        // Gather (опц. для цього кроку)
        public string resourceId;
        public int baseAmount = 3;
    }

    public List<TaskDef> tasks = new();

    public List<TaskInstance> BuildRuntimeTasks()
    {
        var list = new List<TaskInstance>(tasks.Count);

        foreach (var t in tasks)
        {
            string normalizedResourceId = (t.resourceId ?? "").ToLowerInvariant();
            string normalizedRewardResourceId = (t.rewardResourceId ?? "").ToLowerInvariant();
            string normalizedUpfrontCostResourceId = (t.upfrontCostResourceId ?? "").ToLowerInvariant();

            int safeBaseAmount = Mathf.Max(0, t.baseAmount);
            int safeWageGold = Mathf.Max(0, t.wageGold);
            int safeRewardAmount = Mathf.Max(0, t.rewardAmount);
            int safeUpfrontCostAmount = Mathf.Max(0, t.upfrontCostAmount);

            var workBundle = new ResourceBundle();
            if (t.type == TaskType.Gather &&
                !string.IsNullOrWhiteSpace(normalizedResourceId) &&
                safeBaseAmount > 0)
            {
                workBundle.AddExact(normalizedResourceId, safeBaseAmount);
                workBundle.Normalize();
            }

            var rewardBundle = new ResourceBundle();
            if (!string.IsNullOrWhiteSpace(normalizedRewardResourceId) && safeRewardAmount > 0)
            {
                rewardBundle.AddExact(normalizedRewardResourceId, safeRewardAmount);
                rewardBundle.Normalize();
            }
            else if (safeWageGold > 0)
            {
                rewardBundle.AddExact("gold", safeWageGold);
                rewardBundle.Normalize();
            }

            var costBundle = new ResourceBundle();
            if (!string.IsNullOrWhiteSpace(normalizedUpfrontCostResourceId) && safeUpfrontCostAmount > 0)
            {
                costBundle.AddExact(normalizedUpfrontCostResourceId, safeUpfrontCostAmount);
                costBundle.Normalize();
            }

            list.Add(new TaskInstance
            {
                taskId = t.taskId,
                type = t.type,
                displayName = t.displayName,
                active = t.active,
                priority = t.priority,
                maxTakers = t.maxTakers,
                durationSec = t.durationSec,

                // legacy back-compat
                wageGold = normalizedRewardResourceId == "gold" ? safeRewardAmount : safeWageGold,
                resourceId = normalizedResourceId,
                baseAmount = safeBaseAmount,

                // PATCH 9 / PATCH 11
                workOutputBundle = workBundle,
                taskRewardBundle = rewardBundle,
                taskCostBundle = costBundle,

                baseFailChance = t.baseFailChance
            });
        }

        return list;
    }
}
