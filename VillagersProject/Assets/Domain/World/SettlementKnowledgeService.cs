using System;
using System.Collections.Generic;

public class SettlementKnowledgeService
{
    private readonly HashSet<string> _discovered = new();

    public event Action OnChanged;

    public bool IsDiscovered(string spotId)
        => !string.IsNullOrWhiteSpace(spotId) && _discovered.Contains(spotId);

    public void Discover(string spotId)
    {
        if (string.IsNullOrWhiteSpace(spotId)) return;
        if (_discovered.Add(spotId))
            OnChanged?.Invoke();
    }

    public IReadOnlyCollection<string> DiscoveredIds => _discovered;
}