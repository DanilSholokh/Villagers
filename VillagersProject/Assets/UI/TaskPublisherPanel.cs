using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


public class TaskPublisherPanel : MonoBehaviour
{

    [Serializable]
    public class PublishDef
    {
        [Header("UI")]
        public Button button;

        [Header("Task")]
        public TaskType type = TaskType.Gather;
        public string displayName = "Gather";
        public string resourceId = "wood";

        [Min(1)] public int baseAmount = 3;
        [Min(1)] public int maxTakers = 1;
        [Min(0.1f)] public float durationSec = 7f;

        [Min(0)] public int wageGold = 5;

        // PATCH 11: optional exact upfront task cost
        public string upfrontCostResourceId;
        [Min(0)] public int upfrontCostAmount = 0;

        [Range(0f, 1f)] public float baseFailChance = 0f;
        [Range(0, 5)] public int priority = 3;
    }

    [SerializeField] private List<PublishDef> publishButtons = new();

    private TaskBoardService _board;
    private readonly Dictionary<Button, UnityAction> _handlers = new();


    private void Awake()
    {
        // як і раніше, беремо singleton зі сцени через installer
        // (в дампі TaskBoardService створюється в GameInstaller.Awake/Start) :contentReference[oaicite:2]{index=2}
        _board = GameInstaller.TaskBoard;
    }


    public void Bind(TaskBoardService board)
    {
        _board = board;
    }

    private void OnEnable()
    {
        SubscribeButtons();
    }

    private void OnDisable()
    {
        UnsubscribeButtons();
    }


    private void SubscribeButtons()
    {
        UnsubscribeButtons();

        if (publishButtons == null) return;

        foreach (var def in publishButtons)
        {
            if (def == null || def.button == null) continue;

            // важливо: локальна копія, щоб не зловити closure-bug
            var defLocal = def;

            UnityAction h = () => Publish(defLocal);

            _handlers[def.button] = h;
            def.button.onClick.AddListener(h);
        }
    }

    private void UnsubscribeButtons()
    {
        if (_handlers.Count == 0) return;

        foreach (var kv in _handlers)
        {
            if (kv.Key != null)
                kv.Key.onClick.RemoveListener(kv.Value);
        }

        _handlers.Clear();
    }


    private void Publish(PublishDef def)
    {
        if (def == null) return;

        if (_board == null)
            _board = GameInstaller.TaskBoard;

        if (_board == null)
        {
            GameDebug.Error(GameDebugChannel.UI,
            "TaskBoardService is null (GameInstaller not ready?)");
            return;
        }

        // runtime id як у тебе в логах: rt_gather_x_N
        string suffix = def.type switch
        {
            TaskType.Gather => $"gather_{def.resourceId}",
            TaskType.ExploreNewLocation => "explore_new_location",
            TaskType.SurveyKnownLocation => "survey_known_location",
            _ => def.type.ToString().ToLowerInvariant()
        };

        string taskId = $"rt_{suffix}_{UnityEngine.Random.Range(1, 99999)}";
        var locations = GameInstaller.LocationService;

        if (def.type == TaskType.SurveyKnownLocation)
        {
            bool hasKnown = locations != null && locations.HasAnyDiscoveredLocation();
            if (!hasKnown)
            {
                GameDebug.Warning(GameDebugChannel.Task,
                "Locked: no discovered locations yet for SurveyKnownLocation");
                return;
            }
        }

        var workBundle = new ResourceBundle();
        if (def.type == TaskType.Gather &&
            !string.IsNullOrWhiteSpace(def.resourceId) &&
            def.baseAmount > 0)
        {
            workBundle.AddExact(def.resourceId, def.baseAmount);
            workBundle.Normalize();
        }

        var rewardBundle = new ResourceBundle();
        if (def.wageGold > 0)
        {
            rewardBundle.AddExact("gold", def.wageGold);
            rewardBundle.Normalize();
        }

        var costBundle = new ResourceBundle();
        if (!string.IsNullOrWhiteSpace(def.upfrontCostResourceId) && def.upfrontCostAmount > 0)
        {
            costBundle.AddExact(def.upfrontCostResourceId.ToLowerInvariant(), def.upfrontCostAmount);
            costBundle.Normalize();
        }

        var t = new TaskInstance
        {
            taskId = taskId,
            type = def.type,
            displayName = def.displayName,
            active = true,
            priority = def.priority,
            maxTakers = Mathf.Max(1, def.maxTakers),
            durationSec = Mathf.Max(0.1f, def.durationSec),
            baseFailChance = Mathf.Clamp01(def.baseFailChance),

            // legacy back-compat
            wageGold = Mathf.Max(0, def.wageGold),
            resourceId = (def.resourceId ?? "").ToLowerInvariant(),
            baseAmount = Mathf.Max(0, def.baseAmount),

            // PATCH 9 / PATCH 11
            workOutputBundle = workBundle,
            taskRewardBundle = rewardBundle,
            taskCostBundle = costBundle,
        };



        // MVP: resource gate by location discovery
        string resId = (def.resourceId ?? "").ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(resId))
        {

            bool unlocked = locations != null &&
                !string.IsNullOrWhiteSpace(locations.FindAnyLocationForResource(resId, true, true));
            if (!unlocked)
            {
                GameDebug.Warning(GameDebugChannel.Task,
                $"Locked: resource '{resId}' not discovered yet");
                return;
            }
        }

        // якщо Explore — не чіпаємо gather поля (можуть бути пусті)
        if (t.type == TaskType.ExploreNewLocation || t.type == TaskType.SurveyKnownLocation)
        {
            t.resourceId = "";
            t.baseAmount = 0;
        }

        // === Risk model bootstrap ===
        t.successChance = 1f - t.baseFailChance;

        if (t.successChance >= 0.85f) t.riskTier = 0;
        else if (t.successChance >= 0.7f) t.riskTier = 1;
        else if (t.successChance >= 0.55f) t.riskTier = 2;
        else if (t.successChance >= 0.4f) t.riskTier = 3;
        else if (t.successChance >= 0.25f) t.riskTier = 4;
        else t.riskTier = 5;


        // attach to a discovered location (so danger works + deterministic target)
        if (t.type == TaskType.Gather &&
        !string.IsNullOrWhiteSpace(t.resourceId) &&
         string.IsNullOrWhiteSpace(t.targetLocationId))
        {
            string locationId = locations?.FindAnyLocationForResource(t.resourceId, true, true);
            if (!string.IsNullOrWhiteSpace(locationId))
                t.targetLocationId = locationId;
        }

        _board.AddTaskRuntime(t);
    }

    public void PublishGather(
     string displayName,
     string resourceId,
     int baseAmount,
     int maxTakers,
     float durationSec,
     int wageGold,
     int priority,
     float baseFailChance)
    {
        string normalizedResourceId = (resourceId ?? "").ToLowerInvariant();
        var id = $"rt_gather_{normalizedResourceId}_{Time.frameCount}";
        var locations = GameInstaller.LocationService;

        var workBundle = new ResourceBundle();
        if (!string.IsNullOrWhiteSpace(normalizedResourceId) && baseAmount > 0)
        {
            workBundle.AddExact(normalizedResourceId, Mathf.Max(0, baseAmount));
            workBundle.Normalize();
        }

        var rewardBundle = new ResourceBundle();
        if (wageGold > 0)
        {
            rewardBundle.AddExact("gold", Mathf.Max(0, wageGold));
            rewardBundle.Normalize();
        }

        var task = new TaskInstance
        {
            taskId = id,
            type = TaskType.Gather,
            displayName = displayName,

            active = true,
            maxTakers = Mathf.Max(1, maxTakers),
            durationSec = Mathf.Max(0.1f, durationSec),
            wageGold = Mathf.Max(0, wageGold),
            priority = priority,

            resourceId = normalizedResourceId,
            baseAmount = Mathf.Max(0, baseAmount),

            workOutputBundle = workBundle,
            taskRewardBundle = rewardBundle,
            taskCostBundle = new ResourceBundle(),

            baseFailChance = Mathf.Clamp01(baseFailChance)
        };

        bool unlocked = locations != null &&
            !string.IsNullOrWhiteSpace(locations.FindAnyLocationForResource(normalizedResourceId, true, true));
        if (!unlocked)
        {
            GameDebug.Warning(GameDebugChannel.Task,
                $"Locked: resource '{normalizedResourceId}' not discovered yet");
            return;
        }

        task.successChance = 1f - task.baseFailChance;

        if (task.successChance >= 0.85f) task.riskTier = 0;
        else if (task.successChance >= 0.7f) task.riskTier = 1;
        else if (task.successChance >= 0.55f) task.riskTier = 2;
        else if (task.successChance >= 0.4f) task.riskTier = 3;
        else if (task.successChance >= 0.25f) task.riskTier = 4;
        else task.riskTier = 5;

        if (!string.IsNullOrWhiteSpace(task.resourceId) &&
            string.IsNullOrWhiteSpace(task.targetLocationId))
        {
            string locationId = locations?.FindAnyLocationForResource(task.resourceId, true, true);
            if (!string.IsNullOrWhiteSpace(locationId))
                task.targetLocationId = locationId;
        }

        _board.AddTaskRuntime(task);
    }

    public void PublishExploreNewLocation(
    string displayName,
    int maxTakers,
    float durationSec,
    int wageGold,
    int priority,
    float baseFailChance
)
    {
        var id = $"rt_explore_new_{Time.frameCount}";

        var rewardBundle = new ResourceBundle();
        if (wageGold > 0)
        {
            rewardBundle.AddExact("gold", Mathf.Max(0, wageGold));
            rewardBundle.Normalize();
        }

        var task = new TaskInstance
        {
            taskId = id,
            type = TaskType.ExploreNewLocation,
            displayName = displayName,

            active = true,
            maxTakers = Mathf.Max(1, maxTakers),
            durationSec = Mathf.Max(0.1f, durationSec),
            wageGold = Mathf.Max(0, wageGold),
            priority = priority,

            workOutputBundle = new ResourceBundle(),
            taskRewardBundle = rewardBundle,
            taskCostBundle = new ResourceBundle(),

            baseFailChance = Mathf.Clamp01(baseFailChance)
        };

        task.resourceId = "";
        task.baseAmount = 0;

        task.successChance = 1f - task.baseFailChance;

        if (task.successChance >= 0.85f) task.riskTier = 0;
        else if (task.successChance >= 0.7f) task.riskTier = 1;
        else if (task.successChance >= 0.55f) task.riskTier = 2;
        else if (task.successChance >= 0.4f) task.riskTier = 3;
        else if (task.successChance >= 0.25f) task.riskTier = 4;
        else task.riskTier = 5;

        _board.AddTaskRuntime(task);
    }


    public void PublishSurveyKnownLocation(
    string displayName,
    int maxTakers,
    float durationSec,
    int wageGold,
    int priority,
    float baseFailChance)
    {
        var id = $"rt_survey_{Time.frameCount}";
        var locations = GameInstaller.LocationService;

        bool hasKnown = locations != null && locations.HasAnyDiscoveredLocation();
        if (!hasKnown)
        {
            GameDebug.Warning(GameDebugChannel.Task,
                "Locked: no discovered locations yet for SurveyKnownLocation");
            return;
        }

        var rewardBundle = new ResourceBundle();
        if (wageGold > 0)
        {
            rewardBundle.AddExact("gold", Mathf.Max(0, wageGold));
            rewardBundle.Normalize();
        }

        var task = new TaskInstance
        {
            taskId = id,
            type = TaskType.SurveyKnownLocation,
            displayName = displayName,

            active = true,
            maxTakers = Mathf.Max(1, maxTakers),
            durationSec = Mathf.Max(0.1f, durationSec),
            wageGold = Mathf.Max(0, wageGold),
            priority = priority,

            workOutputBundle = new ResourceBundle(),
            taskRewardBundle = rewardBundle,
            taskCostBundle = new ResourceBundle(),

            baseFailChance = Mathf.Clamp01(baseFailChance)
        };

        task.resourceId = "";
        task.baseAmount = 0;

        task.successChance = 1f - task.baseFailChance;

        if (task.successChance >= 0.85f) task.riskTier = 0;
        else if (task.successChance >= 0.7f) task.riskTier = 1;
        else if (task.successChance >= 0.55f) task.riskTier = 2;
        else if (task.successChance >= 0.4f) task.riskTier = 3;
        else if (task.successChance >= 0.25f) task.riskTier = 4;
        else task.riskTier = 5;

        _board.AddTaskRuntime(task);
    }


}