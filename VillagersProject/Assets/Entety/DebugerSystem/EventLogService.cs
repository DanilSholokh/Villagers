using System;
using System.Collections.Generic;

public class EventLogService
{
    public event Action<string> OnPushed;

    private readonly Queue<string> _lines = new();
    private readonly int _cap;

    public EventLogService(int cap = 10)
    {
        _cap = cap;
    }

    public IReadOnlyCollection<string> Lines => _lines;

    public void Push(string msg)
    {
        _lines.Enqueue(msg);
        while (_lines.Count > _cap)
            _lines.Dequeue();

        OnPushed?.Invoke(msg);
    }
}
