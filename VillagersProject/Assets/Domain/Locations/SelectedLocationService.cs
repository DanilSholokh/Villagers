using System;

public sealed class SelectedLocationService
{
    public event Action<string> Changed;

    public string CurrentLocationId { get; private set; }

    public void Set(string locationId)
    {
        if (CurrentLocationId == locationId)
            return;

        CurrentLocationId = locationId;
        Changed?.Invoke(CurrentLocationId);
    }

    public void Clear()
    {
        if (string.IsNullOrEmpty(CurrentLocationId))
            return;

        CurrentLocationId = null;
        Changed?.Invoke(null);
    }
}