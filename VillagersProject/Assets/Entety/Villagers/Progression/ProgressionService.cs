using System;
using System.Collections.Generic;
using UnityEngine;

public class ProgressionService
{
    [Serializable]
    public class VillagerProgress
    {
        public int achievementPoints;
        public int level = 1;

        // базов≥ стати v0.01
        public int strength = 1;
        public int speed = 1;
        public int ingenuity = 1;

        // прост≥ перки €к string id
        public List<string> perks = new();
    }

    public event Action<string, int> OnLevelUp;            // (agentId, newLevel)
    public event Action<string> OnProgressChanged;         // (agentId)

    private readonly Dictionary<string, VillagerProgress> _prog = new();

    private static readonly string[] PERK_POOL =
    {
        "StrongGrip",
        "FastLearner",
        "HardWorker",
        "Lucky",
        "Stubborn"
    };

    public VillagerProgress Get(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId)) agentId = "unknown";

        if (!_prog.TryGetValue(agentId, out var p))
        {
            p = new VillagerProgress();
            _prog[agentId] = p;
        }

        return p;
    }

    public void EnsureInitialized(string agentId, int baseStr = 1, int baseSpd = 1, int baseIng = 1)
    {
        var p = Get(agentId);
        p.strength = Mathf.Max(1, baseStr);
        p.speed = Mathf.Max(1, baseSpd);
        p.ingenuity = Mathf.Max(1, baseIng);

        if (p.perks == null) p.perks = new List<string>();
        OnProgressChanged?.Invoke(agentId);
    }

    public void AddAchievement(string agentId, int points)
    {
        if (points <= 0) return;

        var p = Get(agentId);
        p.achievementPoints += points;

        // v0.01: 10 points = +1 level
        int targetLevel = 1 + (p.achievementPoints / 10);

        while (p.level < targetLevel)
        {
            p.level++;

            // v0.01: на кожен левел легкий р≥ст стат≥в
            p.strength += 1;
            p.speed += 1;
            p.ingenuity += 1;

            // v0.01: кожн≥ 2 левели Ч перк
            if (p.level % 2 == 0)
            {
                TryAddRandomPerk(agentId);
            }

            OnLevelUp?.Invoke(agentId, p.level);
            Debug.Log($"[Progression] agent={agentId} LEVEL UP -> {p.level} (ap={p.achievementPoints})");
        }

        OnProgressChanged?.Invoke(agentId);
    }

    public bool HasPerk(string agentId, string perkId)
    {
        var p = Get(agentId);
        if (p.perks == null) return false;
        return p.perks.Contains(perkId);
    }

    public bool AddPerk(string agentId, string perkId)
    {
        if (string.IsNullOrWhiteSpace(perkId)) return false;

        var p = Get(agentId);
        if (p.perks == null) p.perks = new List<string>();

        if (p.perks.Contains(perkId)) return false;
        p.perks.Add(perkId);

        OnProgressChanged?.Invoke(agentId);
        return true;
    }

    private void TryAddRandomPerk(string agentId)
    {
        var p = Get(agentId);
        if (p.perks == null) p.perks = new List<string>();

        // простий пул без дублюванн€
        for (int i = 0; i < 8; i++)
        {
            var perk = PERK_POOL[UnityEngine.Random.Range(0, PERK_POOL.Length)];
            if (!p.perks.Contains(perk))
            {
                p.perks.Add(perk);
                Debug.Log($"[Progression] agent={agentId} gained perk={perk}");
                return;
            }
        }
    }
}