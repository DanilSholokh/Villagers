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
            list.Add(new TaskInstance
            {
                taskId = t.taskId,
                type = t.type,
                displayName = t.displayName,
                active = t.active,
                priority = t.priority,
                maxTakers = t.maxTakers,
                durationSec = t.durationSec,

                wageGold = t.wageGold,

                resourceId = t.resourceId,
                baseAmount = t.baseAmount,

                baseFailChance = t.baseFailChance
            });
        }
        return list;
    }
}
