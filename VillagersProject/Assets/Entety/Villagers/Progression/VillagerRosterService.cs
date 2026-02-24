using System;
using System.Collections.Generic;

public class VillagerRosterService
{
    public event Action<string> OnVillagerChanged; // agentId
    public event Action OnRosterChanged;

    private readonly Dictionary<string, VillagerState> _states = new();

    public IReadOnlyDictionary<string, VillagerState> GetAll() => _states;

    public VillagerState GetOrCreate(string agentId, string displayName = null)
    {
        if (string.IsNullOrWhiteSpace(agentId)) agentId = "unknown";

        if (!_states.TryGetValue(agentId, out var s))
        {
            s = new VillagerState
            {
                agentId = agentId,
                displayName = string.IsNullOrWhiteSpace(displayName) ? agentId : displayName,
                status = VillagerStatus.Idle,
                lastChangeUtc = DateTime.UtcNow
            };
            _states[agentId] = s;
            OnRosterChanged?.Invoke();
            OnVillagerChanged?.Invoke(agentId);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                s.displayName = displayName;
        }

        return s;
    }

    public void SetStatus(string agentId, VillagerStatus status, string taskId = null, string taskName = null)
    {
        var s = GetOrCreate(agentId);
        s.status = status;
        s.taskId = taskId;
        s.taskName = taskName;
        s.lastChangeUtc = DateTime.UtcNow;

        OnVillagerChanged?.Invoke(agentId);
    }
}