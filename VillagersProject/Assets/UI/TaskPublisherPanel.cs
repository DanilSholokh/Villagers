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


    private void WireButtons()
    {
        if (publishButtons == null) return;

        foreach (var def in publishButtons)
        {
            if (def == null || def.button == null) continue;

            def.button.onClick.RemoveListener(() => Publish(def));
            def.button.onClick.AddListener(() => Publish(def));
        }
    }

    private void UnwireButtons()
    {
        if (publishButtons == null) return;

        foreach (var def in publishButtons)
        {
            if (def == null || def.button == null) continue;
            def.button.onClick.RemoveListener(() => Publish(def));
        }
    }

    private void Publish(PublishDef def)
    {
        if (def == null) return;

        if (_board == null)
            _board = GameInstaller.TaskBoard;

        if (_board == null)
        {
            Debug.LogError("[TaskPublisherPanel] TaskBoardService is null (GameInstaller not ready?)");
            return;
        }

        // runtime id як у тебе в логах: rt_gather_x_N
        string suffix = $"{def.type.ToString().ToLowerInvariant()}_{(def.type == TaskType.Gather ? def.resourceId : "explore")}";
        string taskId = $"rt_{suffix}_{UnityEngine.Random.Range(1, 99999)}";

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
            wageGold = Mathf.Max(0, def.wageGold),

            // gather-only
            resourceId = (def.resourceId ?? "").ToLowerInvariant(),
            baseAmount = Mathf.Max(0, def.baseAmount),
        };


        // MVP: resource gate by discovery
        string resId = def.resourceId; // або звідки ти береш resourceId у дефініції
        if (!string.IsNullOrWhiteSpace(resId))
        {
            var reg = GameInstaller.ExploreRegistry;
            var know = GameInstaller.Knowledge;

            bool unlocked = reg != null && reg.HasDiscoveredResource(know, resId);
            if (!unlocked)
            {
                Debug.Log($"[Publisher] Locked: resource '{resId}' not discovered yet");
                return;
            }
        }

        // якщо Explore — не чіпаємо gather поля (можуть бути пусті)
        if (t.type == TaskType.Explore)
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
        if (!string.IsNullOrWhiteSpace(t.resourceId) && string.IsNullOrWhiteSpace(t.targetSpotId))
        {
            var reg = GameInstaller.ExploreRegistry;
            var know = GameInstaller.Knowledge;
            string spotId = reg?.GetAnyDiscoveredSpotIdForResource(know, t.resourceId);
            if (!string.IsNullOrWhiteSpace(spotId))
                t.targetSpotId = spotId;
        }

        _board.AddTaskRuntime(t);
    }

    // ---- ORIGINAL API залишаємо, щоб не ламати інше ----

    public void PublishGather(
        string displayName,
        string resourceId,
        int baseAmount,
        int maxTakers,
        float durationSec,
        int wageGold,
        int priority,
        float baseFailChance
    )
    {
        var id = $"rt_gather_{resourceId}_{Time.frameCount}";

        var task = new TaskInstance
        {
            taskId = id,
            type = TaskType.Gather,
            displayName = displayName,

            active = true,
            maxTakers = maxTakers,
            durationSec = durationSec,
            wageGold = wageGold,
            priority = priority,

            resourceId = resourceId,
            baseAmount = baseAmount,

            baseFailChance = baseFailChance
        };

        _board.AddTaskRuntime(task);
    }

    public void PublishExplore(
        string displayName,
        int maxTakers,
        float durationSec,
        int wageGold,
        int priority,
        float baseFailChance
    )
    {
        var id = $"rt_explore_{Time.frameCount}";

        var task = new TaskInstance
        {
            taskId = id,
            type = TaskType.Explore,
            displayName = displayName,

            active = true,
            maxTakers = maxTakers,
            durationSec = durationSec,
            wageGold = wageGold,
            priority = priority,

            baseFailChance = baseFailChance
        };

        _board.AddTaskRuntime(task);
    }
}