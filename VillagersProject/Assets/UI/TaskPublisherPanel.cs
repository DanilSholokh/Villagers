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
    
    [SerializeField] private TMPro.TextMeshProUGUI publishStateText;

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

        bool anyPublishable = false;
        string firstBlockedReason = string.Empty;

        for (int i = 0; i < publishButtons.Count; i++)
        {
            var def = publishButtons[i];
            if (def == null || def.button == null)
                continue;

            string reason;
            bool canPublish = CanPublish(def, out reason);
            def.button.interactable = canPublish;

            if (canPublish)
                anyPublishable = true;
            else if (string.IsNullOrWhiteSpace(firstBlockedReason))
                firstBlockedReason = reason;
        }

        if (publishStateText != null)
            publishStateText.text = BuildPublishStateText(anyPublishable, firstBlockedReason);
    }

    private string BuildPublishStateText(bool anyPublishable, string blockedReason)
    {
        if (anyPublishable)
            return "Ready";

        return string.IsNullOrWhiteSpace(blockedReason)
            ? "Blocked"
            : $"Blocked: {blockedReason}";
    }



    private bool TryResolvePublishTargetLocation(
    PublishDef def,
    out string targetLocationId,
    out string reason)
    {
        targetLocationId = string.Empty;
        reason = string.Empty;

        if (def == null)
        {
            reason = "Definition is null";
            return false;
        }

        var locations = GameInstaller.LocationService;
        if (locations == null)
        {
            reason = "LocationService is null";
            return false;
        }

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

                    targetLocationId = locations.FindRandomLocationForResource(
                        normalizedResourceId,
                        onlyDiscovered: true,
                        onlyUnlocked: true
                    );

                    if (string.IsNullOrWhiteSpace(targetLocationId))
                    {
                        reason = $"Resource '{normalizedResourceId}' not discovered yet";
                        return false;
                    }

                    return true;
                }

            case TaskType.ExploreNewLocation:
                {
                    targetLocationId = locations.FindRandomUnknownLocationId();

                    if (string.IsNullOrWhiteSpace(targetLocationId))
                    {
                        reason = "No unknown locations left";
                        return false;
                    }

                    return true;
                }

            case TaskType.SurveyKnownLocation:
                {
                    targetLocationId = locations.FindRandomDiscoveredLocationWithPotentialResource();

                    if (string.IsNullOrWhiteSpace(targetLocationId))
                        targetLocationId = locations.FindRandomDiscoveredLocationId();

                    if (string.IsNullOrWhiteSpace(targetLocationId))
                    {
                        reason = "No discovered locations yet";
                        return false;
                    }

                    return true;
                }

            default:
                reason = $"Unsupported publish type: {def.type}";
                return false;
        }
    }

    private bool TryValidatePublishRequest(
        PublishDef def,
        out string targetLocationId,
        out string reason)
    {
        targetLocationId = string.Empty;
        reason = string.Empty;

        if (def == null)
        {
            reason = "Definition is null";
            return false;
        }

        if (_board == null && GameInstaller.TaskBoard == null)
        {
            reason = "TaskBoardService is null";
            return false;
        }

        if (!TryResolvePublishTargetLocation(def, out targetLocationId, out reason))
            return false;

        var treasury = GameInstaller.Treasury;

        switch (def.type)
        {
            case TaskType.Gather:
            case TaskType.SurveyKnownLocation:
                {
                    var costBundle = BuildExactUpfrontCostBundle(def);

                    if (treasury != null && costBundle != null && !costBundle.IsEmpty)
                    {
                        var validateResult = treasury.ValidateBundle(costBundle, "publish_preview");
                        if (!validateResult.success)
                        {
                            reason = string.IsNullOrWhiteSpace(validateResult.message)
                                ? "Not enough upfront resources"
                                : validateResult.message;
                            return false;
                        }
                    }

                    return true;
                }

            case TaskType.ExploreNewLocation:
                {
                    var explore = GameInstaller.ExplorationUnlock;
                    if (explore == null)
                    {
                        reason = "ExplorationUnlockService is null";
                        return false;
                    }

                    if (treasury != null && !explore.CanAfford(treasury, out reason))
                        return false;

                    return true;
                }

            default:
                reason = $"Unsupported publish type: {def.type}";
                return false;
        }
    }


    private bool CanPublish(PublishDef def, out string reason)
    {
        return TryValidatePublishRequest(def, out _, out reason);
    }

    private void Publish(PublishDef def)
    {
        _board ??= GameInstaller.TaskBoard;

        if (_board == null)
        {
            GameDebug.Error(GameDebugChannel.UI, "TaskBoardService is null (GameInstaller not ready?)");
            return;
        }

        if (!TryValidatePublishRequest(def, out string targetLocationId, out string reason))
        {
            GameDebug.Warning(GameDebugChannel.Task, $"Publish blocked: {reason}");
            return;
        }

        switch (def.type)
        {
            case TaskType.Gather:
                PublishGather(
                    def.displayName,
                    def.resourceId,
                    def.baseAmount,
                    def.maxTakers,
                    def.durationSec,
                    def.wageGold,
                    def.priority,
                    def.baseFailChance,
                    def.upfrontCostResourceId,
                    def.upfrontCostAmount,
                    targetLocationId
                );
                break;

            case TaskType.ExploreNewLocation:
                PublishExploreTask(def);
                break;

            case TaskType.SurveyKnownLocation:
                PublishSurveyKnownLocation(
                    def.displayName,
                    def.maxTakers,
                    def.durationSec,
                    def.wageGold,
                    def.priority,
                    def.baseFailChance,
                    def.upfrontCostResourceId,
                    def.upfrontCostAmount,
                    targetLocationId
                );
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
    int upfrontCostAmount = 0,
    string forcedTargetLocationId = null)
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


        task.RecalculateDerivedStats();

        if (!string.IsNullOrWhiteSpace(forcedTargetLocationId))
        {
            task.SetTargetLocation(forcedTargetLocationId);
        }
        else if (!string.IsNullOrWhiteSpace(task.resourceId) && !task.HasTargetLocation())
        {
            string locationId = locations?.FindAnyLocationForResource(task.resourceId, true, true);
            if (!string.IsNullOrWhiteSpace(locationId))
                task.SetTargetLocation(locationId);
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
    int upfrontCostAmount = 0,
    string forcedTargetLocationId = null)
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

        task.RecalculateDerivedStats();

        if (!string.IsNullOrWhiteSpace(forcedTargetLocationId))
        {
            task.SetTargetLocation(forcedTargetLocationId);
        }
        else
        {
            string targetLocationId = locations.FindRandomDiscoveredLocationWithPotentialResource();

            if (string.IsNullOrWhiteSpace(targetLocationId))
                targetLocationId = locations.FindRandomDiscoveredLocationId();

            if (!string.IsNullOrWhiteSpace(targetLocationId))
                task.SetTargetLocation(targetLocationId);
        }

        _board.AddTaskRuntime(task);
    }

    public void PublishExploreNewLocation(
    string displayName,
    int maxTakers,
    float durationSec,
    int wageGold,
    int priority,
    float baseFailChance,
    string forcedTargetLocationId = null)
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

            resourceId = string.Empty,
            baseAmount = 0,

            workOutputBundle = new ResourceBundle(),
            taskRewardBundle = rewardBundle,
            taskCostBundle = new ResourceBundle(),

            baseFailChance = Mathf.Clamp01(baseFailChance)
        };


        task.RecalculateDerivedStats();

        string targetLocationId = locations.FindRandomUnknownLocationId();
        if (!string.IsNullOrWhiteSpace(targetLocationId))
            task.SetTargetLocation(targetLocationId);

        _board.AddTaskRuntime(task);

        GameDebug.Info(
            GameDebugChannel.UI,
            $"Published Explore task: {task.taskId} cost={explore.GetCostText()}"
        );
    }

    public void ValidateBootstrapConfig()
    {
        if (publishButtons == null || publishButtons.Count == 0)
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                $"TaskPublisherPanel '{name}' has no publish button definitions."
            );
            return;
        }

        for (int i = 0; i < publishButtons.Count; i++)
        {
            var def = publishButtons[i];
            if (def == null)
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskPublisherPanel '{name}' has null PublishDef at index {i}."
                );
                continue;
            }

            if (def.button == null)
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskPublisherPanel '{name}' PublishDef '{def.displayName}' has null Button."
                );
            }

            if (string.IsNullOrWhiteSpace(def.displayName))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskPublisherPanel '{name}' has PublishDef with empty displayName at index {i}."
                );
            }

            if (def.maxTakers < 1)
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskPublisherPanel '{name}' PublishDef '{def.displayName}' has invalid maxTakers={def.maxTakers}."
                );
            }

            if (def.durationSec < 0.1f)
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskPublisherPanel '{name}' PublishDef '{def.displayName}' has invalid durationSec={def.durationSec}."
                );
            }

            string normalizedResourceId = Normalize(def.resourceId);
            string normalizedCostId = Normalize(def.upfrontCostResourceId);

            switch (def.type)
            {
                case TaskType.Gather:
                    if (string.IsNullOrWhiteSpace(normalizedResourceId))
                    {
                        GameDebug.Warning(
                            GameDebugChannel.General,
                            $"TaskPublisherPanel '{name}' Gather PublishDef '{def.displayName}' has empty resourceId."
                        );
                    }
                    else if (GameInstaller.Resources != null && !GameInstaller.Resources.Has(normalizedResourceId))
                    {
                        GameDebug.Warning(
                            GameDebugChannel.General,
                            $"TaskPublisherPanel '{name}' Gather PublishDef '{def.displayName}' references missing resourceId '{normalizedResourceId}'."
                        );
                    }
                    break;

                case TaskType.ExploreNewLocation:
                    if (!string.IsNullOrWhiteSpace(normalizedCostId) || def.upfrontCostAmount > 0)
                    {
                        GameDebug.Warning(
                            GameDebugChannel.General,
                            $"TaskPublisherPanel '{name}' Explore PublishDef '{def.displayName}' has exact upfront cost fields set, but Explore uses ExplorationUnlockRuleSO as source of truth."
                        );
                    }
                    break;

                case TaskType.SurveyKnownLocation:
                    break;
            }

            if (!string.IsNullOrWhiteSpace(normalizedCostId) &&
                GameInstaller.Resources != null &&
                !GameInstaller.Resources.Has(normalizedCostId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskPublisherPanel '{name}' PublishDef '{def.displayName}' references missing upfrontCostResourceId '{normalizedCostId}'."
                );
            }
        }
    }


    private void PublishExploreTask(PublishDef def)
    {
        var board = _board != null ? _board : GameInstaller.TaskBoard;
        var locations = GameInstaller.LocationService;
        var explore = GameInstaller.ExplorationUnlock;

        if (board == null)
        {
            GameDebug.Error(GameDebugChannel.UI, "TaskBoardService is null (GameInstaller not ready?)");
            return;
        }

        if (locations == null)
        {
            GameDebug.Error(GameDebugChannel.UI, "LocationService is null");
            return;
        }

        if (explore == null)
        {
            GameDebug.Error(GameDebugChannel.UI, "ExplorationUnlockService is null");
            return;
        }

        if (GameInstaller.Treasury != null && !explore.CanAfford(GameInstaller.Treasury, out var blockedReason))
        {
            GameDebug.Warning(GameDebugChannel.Task, $"Publish blocked: {blockedReason}");
            return;
        }

        string targetLocationId = locations.FindRandomUnknownLocationId();
        if (string.IsNullOrWhiteSpace(targetLocationId))
        {
            GameDebug.Warning(GameDebugChannel.Task, "Publish blocked: No unknown locations left");
            return;
        }

        var rewardBundle = new ResourceBundle();
        if (def.wageGold > 0)
        {
            rewardBundle.AddExact("gold", Mathf.Max(0, def.wageGold));
            rewardBundle.Normalize();
        }

        var task = new TaskInstance
        {
            taskId = System.Guid.NewGuid().ToString("N"),
            type = TaskType.ExploreNewLocation,
            displayName = string.IsNullOrWhiteSpace(def.displayName) ? explore.DisplayName : def.displayName,
            active = true,
            priority = Mathf.Clamp(def.priority, 0, 5),
            maxTakers = Mathf.Max(1, def.maxTakers),
            durationSec = Mathf.Max(0.1f, def.durationSec),
            wageGold = Mathf.Max(0, def.wageGold),
            resourceId = string.Empty,
            baseAmount = 0,
            taskCostBundle = explore.BuildCostBundle(),
            workOutputBundle = new ResourceBundle(),
            taskRewardBundle = rewardBundle,
            baseFailChance = Mathf.Clamp01(def.baseFailChance)
        };

        task.RecalculateDerivedStats();
        task.SetTargetLocation(targetLocationId);

        board.AddTaskRuntime(task);

        GameDebug.Info(
            GameDebugChannel.UI,
            $"Published Explore task: {task.taskId} target={targetLocationId} cost={explore.GetCostText()}"
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