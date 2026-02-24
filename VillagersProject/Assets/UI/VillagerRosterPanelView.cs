using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VillagerRosterPanelView : MonoBehaviour
{
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private VillagerRowUI rowPrefab;

    private VillagerRosterService _roster;
    private ProgressionService _prog;

    private readonly List<VillagerRowUI> _rows = new();

    public void Bind(VillagerRosterService roster, ProgressionService prog)
    {
        // відписка
        if (_roster != null)
        {
            _roster.OnRosterChanged -= HandleRosterChanged;
            _roster.OnVillagerChanged -= HandleVillagerChanged;
        }
        if (_prog != null)
        {
            _prog.OnProgressChanged -= HandleProgressChanged;
        }

        _roster = roster;
        _prog = prog;

        // підписка
        if (_roster != null)
        {
            _roster.OnRosterChanged += HandleRosterChanged;
            _roster.OnVillagerChanged += HandleVillagerChanged;
        }
        if (_prog != null)
        {
            _prog.OnProgressChanged += HandleProgressChanged;
        }

        Rebuild();
        RefreshAll();
    }

    private void OnEnable()
    {
        if (_roster != null)
        {
            _roster.OnRosterChanged += HandleRosterChanged;
            _roster.OnVillagerChanged += HandleVillagerChanged;
        }

        if (_prog != null)
        {
            _prog.OnProgressChanged += HandleProgressChanged;
        }

        Rebuild();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (_roster != null)
        {
            _roster.OnRosterChanged -= HandleRosterChanged;
            _roster.OnVillagerChanged -= HandleVillagerChanged;
        }

        if (_prog != null)
        {
            _prog.OnProgressChanged -= HandleProgressChanged;
        }
    }

    private void HandleRosterChanged()
    {
        Rebuild();
        RefreshAll();
    }

    private void HandleVillagerChanged(string agentId)
    {
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i] != null && _rows[i].AgentId == agentId)
                _rows[i].Refresh(_roster, _prog);
    }

    private void HandleProgressChanged(string agentId)
    {
        HandleVillagerChanged(agentId);
    }

    private void Rebuild()
    {
        if (rowsRoot == null || rowPrefab == null) return;
        if (_roster == null) return;

        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i] != null) Destroy(_rows[i].gameObject);
        _rows.Clear();

        var ordered = _roster.GetAll().Values.OrderBy(v => v.displayName).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var row = Instantiate(rowPrefab, rowsRoot);
            row.Bind(ordered[i].agentId);
            _rows.Add(row);
        }
    }

    private void RefreshAll()
    {
        if (_roster == null) return;

        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i] != null) _rows[i].Refresh(_roster, _prog);
    }
}