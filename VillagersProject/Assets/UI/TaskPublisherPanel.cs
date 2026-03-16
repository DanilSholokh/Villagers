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

        [Header("Optional exact upfront task cost")]
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
        _board = GameInstaller.TaskBoard;
    }

    public void Bind(TaskBoardService board)
    {
        _board = board;
        RefreshButtons();
    }

    private void OnEnable()
    {
        SubscribeButtons();
        RefreshButtons();
    }

    private void OnDisable()
    {
        UnsubscribeButtons();
    }

    private void Update()
    {
        RefreshButtons();
    }

    private void SubscribeButtons()
    {
        UnsubscribeButtons();

        if (publishButtons == null)
            return;

        foreach (var def in publishButtons)
        {
            if (def == null || def.button == null)
                continue;

            var defLocal = def;
            UnityAction h = () => Publish(defLocal);

            _handlers[def.button] = h;
            def.button.onClick.AddListener(h);
        }
    }

    private void UnsubscribeButtons()
    {
        if (_handlers.Count == 0)
            return;

        foreach (var kv in _handlers)
        {
            if (kv.Key != null)
                kv.Key.onClick.RemoveListener(kv.Value);
        }

        _handlers.Clear();
    }



    private void RefreshButtons()
    {
        if (publishButtons == null)
            return;

        for (int i = 0; i < publishButtons.Count; i++)
        {
            var def = publishButtons[i];
            if (def == null || def.button == null)
                continue;

            def.button.interactable = CanPublish(def, out _);
        }
    }

    private bool CanPublish(PublishDef def, out string reason)
    {
        reason = string.Empty;

        if (def == null)
        {
            reason = "Definition is null";
            return false;
        }

        var treasury = GameInstaller.Treasury;
        var locations = GameInstaller.LocationService;

        switch (def.type)
        {
            case TaskType.Gather:
                {
                    string normalizedResourceId = Normalize(def.resourceId);
                    if (string.IsNullOrWhiteSpace(normalizedResourceId))
                    {
                        reason = "Gather resourceId is empty";
                        return false;
                    }

                    bool unlocked = locations != null &&
                        !string.IsNullOrWhiteSpace(locations.FindAnyLocationForResource(normalizedResourceId, true, true));

                    if (!unlocked)
                    {
                        reason = $"Resource '{normalizedResourceId}' not discovered yet";
                        return false;
                    }

                    var costBundle = BuildExactUpfrontCostBundle(def);
                    if (treasury != null && costBundle != null && !costBundle.IsEmpty && !treasury.CanAfford(costBundle))
                    {
                        reason = "Not enough upfront resources";
                        return false;
                    }

                    return true;
                }

            case TaskType.ExploreNewLocation:
                {
                    bool hasUnknown = locations != null &&
                        !string.IsNullOrWhiteSpace(locations.FindRandomUnknownLocationId());

                    if (!hasUnknown)
                    {
                        reason = "No unknown locations left";
                        return false;
                    }

                    var explore = GameInstaller.ExplorationUnlock;
                    if (explore == null)
                    {
                        reason = "Exploration rule is not configured";
                        return false;
                    }

                    if (!explore.CanAfford(treasury, out reason))
                        return false;

                    return true;
                }

            case TaskType.SurveyKnownLocation:
                {
                    bool hasKnown = locations != null && locations.HasAnyDiscoveredLocation();
                    if (!hasKnown)
                    {
                        reason = "No discovered locations yet";
                        return false;
                    }

                    var costBundle = BuildExactUpfrontCostBundle(def);
                    if (treasury != null && costBundle != null && !costBundle.IsEmpty && !treasury.CanAfford(costBundle))
                    {
                        reason = "Not enough upfront resources";
                        return false;
                    }

                    return true;
                }
        }

        return true;
    }

    private void Publish(PublishDef def)
    {
        if (def == null)
            return;

        if (_board == null)
            _board = GameInstaller.TaskBoard;

        if (_board == null)
        {
            GameDebug.Error(GameDebugChannel.UI, "TaskBoardService is null (GameInstaller not ready?)");
            return;
        }

        if (!CanPublish(def, out string reason))
        {
            GameDebug.Warning(GameDebugChannel.Task, $"Publish blocked: {reason}");
            return;
        }

        switch (def.type)
        {
            case TaskType.Gather:
                PublishGather(def.displayName, def.resourceId, def.baseAmount, def.maxTakers, def.durationSec, def.wageGold, def.priority, def.baseFailChance, def.upfrontCostResourceId, def.upfrontCostAmount);
                break;

            case TaskType.ExploreNewLocation:
                PublishExploreNewLocation(def.displayName, def.maxTakers, def.durationSec, def.wageGold, def.priority, def.baseFailChance);
                break;

            case TaskType.SurveyKnownLocation:
                PublishSurveyKnownLocation(def.displayName, def.maxTakers, def.durationSec, def.wageGold, def.priority, def.baseFailChance, def.upfrontCostResourceId, def.upfrontCostAmount);
                break;

            default:
                GameDebug.Warning(GameDebugChannel.UI, $"Unsupported publish type: {def.type}");
                break;
        }
    }

    public void PublishGather(
        string displayName,
        string resourceId,
        int baseAmount,
        int maxTakers,
        float durationSec,
        int wageGold,
        int priority,
        float baseFailChance,
        string upfrontCostResourceId = null,
        int upfrontCostAmount = 0)
    {
        string normalizedResourceId = Normalize(resourceId);
        var id = $"rt_gather_{normalizedResourceId}_{Time.frameCount}";
        var locations = GameInstaller.LocationService;

        bool unlocked = locations != null &&
            !string.IsNullOrWhiteSpace(locations.FindAnyLocationForResource(normalizedResourceId, true, true));
        if (!unlocked)
        {
            GameDebug.Warning(GameDebugChannel.Task,
                $"Locked: resource '{normalizedResourceId}' not discovered yet");
            return;
        }

        var costBundle = new ResourceBundle();
        string normalizedUpfrontCostResourceId = Normalize(upfrontCostResourceId);
        if (!string.IsNullOrWhiteSpace(normalizedUpfrontCostResourceId) && upfrontCostAmount > 0)
        {
            costBundle.AddExact(normalizedUpfrontCostResourceId, Mathf.Max(0, upfrontCostAmount));
            costBundle.Normalize();
        }

        if (GameInstaller.Treasury != null && !costBundle.IsEmpty && !GameInstaller.Treasury.CanAfford(costBundle))
        {
            GameDebug.Warning(GameDebugChannel.Task, "Blocked: not enough upfront resources for gather task");
            return;
        }

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
            taskCostBundle = costBundle,

            baseFailChance = Mathf.Clamp01(baseFailChance)
        };

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

    public void PublishSurveyKnownLocation(
        string displayName,
        int maxTakers,
        float durationSec,
        int wageGold,
        int priority,
        float baseFailChance,
        string upfrontCostResourceId = null,
        int upfrontCostAmount = 0)
    {
        var id = $"rt_survey_{Time.frameCount}";
        var locations = GameInstaller.LocationService;

        bool hasKnown = locations != null && locations.HasAnyDiscoveredLocation();
        if (!hasKnown)
        {
            GameDebug.Warning(GameDebugChannel.Task, "Locked: no discovered locations yet for SurveyKnownLocation");
            return;
        }

        var costBundle = new ResourceBundle();
        string normalizedUpfrontCostResourceId = Normalize(upfrontCostResourceId);
        if (!string.IsNullOrWhiteSpace(normalizedUpfrontCostResourceId) && upfrontCostAmount > 0)
        {
            costBundle.AddExact(normalizedUpfrontCostResourceId, Mathf.Max(0, upfrontCostAmount));
            costBundle.Normalize();
        }

        if (GameInstaller.Treasury != null && !costBundle.IsEmpty && !GameInstaller.Treasury.CanAfford(costBundle))
        {
            GameDebug.Warning(GameDebugChannel.Task, "Blocked: not enough upfront resources for SurveyKnownLocation");
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

            resourceId = string.Empty,
            baseAmount = 0,

            workOutputBundle = new ResourceBundle(),
            taskRewardBundle = rewardBundle,
            taskCostBundle = costBundle,

            baseFailChance = Mathf.Clamp01(baseFailChance)
        };

        task.successChance = 1f - task.baseFailChance;

        if (task.successChance >= 0.85f) task.riskTier = 0;
        else if (task.successChance >= 0.7f) task.riskTier = 1;
        else if (task.successChance >= 0.55f) task.riskTier = 2;
        else if (task.successChance >= 0.4f) task.riskTier = 3;
        else if (task.successChance >= 0.25f) task.riskTier = 4;
        else task.riskTier = 5;

        _board.AddTaskRuntime(task);
    }

    public void PublishExploreNewLocation(
    string displayName = "Discover Region",
    int maxTakers = 1,
    float durationSec = 10f,
    int wageGold = 0,
    int priority = 3,
    float baseFailChance = 0.15f)
    {
        if (_board == null)
            _board = GameInstaller.TaskBoard;

        if (_board == null)
        {
            GameDebug.Error(GameDebugChannel.UI, "TaskBoardService is null (GameInstaller not ready?)");
            return;
        }

        var locations = GameInstaller.LocationService;
        if (locations == null)
        {
            GameDebug.Error(GameDebugChannel.UI, "LocationService is null");
            return;
        }

        bool hasUnknown = !string.IsNullOrWhiteSpace(locations.FindRandomUnknownLocationId());
        if (!hasUnknown)
        {
            GameDebug.Warning(GameDebugChannel.UI, "Publish Explore blocked: no unknown locations left");
            return;
        }

        var explore = GameInstaller.ExplorationUnlock;
        if (explore == null)
        {
            GameDebug.Error(GameDebugChannel.UI, "ExplorationUnlockService is null");
            return;
        }

        if (!explore.CanAfford(GameInstaller.Treasury, out var reason))
        {
            GameDebug.Warning(GameDebugChannel.UI, $"Publish Explore blocked: {reason}");
            return;
        }

        string taskId = $"rt_explore_new_location_{UnityEngine.Random.Range(1000, 9999)}";

        var rewardBundle = new ResourceBundle();
        if (wageGold > 0)
        {
            rewardBundle.AddExact("gold", wageGold);
            rewardBundle.Normalize();
        }

        var costBundle = explore.BuildCostBundle();

        var task = new TaskInstance
        {
            taskId = taskId,
            type = TaskType.ExploreNewLocation,
            displayName = string.IsNullOrWhiteSpace(displayName) ? explore.DisplayName : displayName,

            active = true,
            maxTakers = Mathf.Max(1, maxTakers),
            durationSec = Mathf.Max(0.1f, durationSec),
            wageGold = Mathf.Max(0, wageGold),
            priority = Mathf.Clamp(priority, 0, 5),

            targetLocationId = string.Empty,
            resourceId = string.Empty,
            baseAmount = 0,

            workOutputBundle = new ResourceBundle(),
            taskRewardBundle = rewardBundle,
            taskCostBundle = costBundle,

            baseFailChance = Mathf.Clamp01(baseFailChance)
        };

        task.successChance = 1f - task.baseFailChance;

        if (task.successChance >= 0.85f) task.riskTier = 0;
        else if (task.successChance >= 0.7f) task.riskTier = 1;
        else if (task.successChance >= 0.55f) task.riskTier = 2;
        else if (task.successChance >= 0.4f) task.riskTier = 3;
        else if (task.successChance >= 0.25f) task.riskTier = 4;
        else task.riskTier = 5;

        _board.AddTaskRuntime(task);

        GameDebug.Info(
            GameDebugChannel.UI,
            $"Published Explore task: {task.taskId} cost={explore.GetCostText()}"
        );
    }

    private ResourceBundle BuildExactUpfrontCostBundle(PublishDef def)
    {
        var bundle = new ResourceBundle();

        if (def == null)
            return bundle;

        string normalized = Normalize(def.upfrontCostResourceId);
        if (!string.IsNullOrWhiteSpace(normalized) && def.upfrontCostAmount > 0)
        {
            bundle.AddExact(normalized, Mathf.Max(0, def.upfrontCostAmount));
            bundle.Normalize();
        }

        return bundle;
    }

    private string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}