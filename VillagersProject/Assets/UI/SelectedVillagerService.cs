using System;

public class SelectedVillagerService
{

    public event Action<string> OnSelectedChanged; // agentId (null/"" = none)

    private string _selectedId;

    public string SelectedId => _selectedId;

    public void SetSelected(string agentId)
    {
        if (_selectedId == agentId) return;
        _selectedId = agentId;
        OnSelectedChanged?.Invoke(_selectedId);
    }

    public void Clear() => SetSelected(null);

}