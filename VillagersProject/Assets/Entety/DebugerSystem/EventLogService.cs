using System;
using System.Collections.Generic;
using System.Text;

public sealed class EventLogService
{
    private readonly int _capacity;
    private readonly Queue<EventLogEntry> _entries;

    public event Action Changed;

    public EventLogService(int capacity)
    {
        _capacity = Math.Max(1, capacity);
        _entries = new Queue<EventLogEntry>(_capacity);

        GameDebug.OnMessage += HandleDebugMessage;
    }

    public IReadOnlyCollection<EventLogEntry> Entries => _entries;

    public void Dispose()
    {
        GameDebug.OnMessage -= HandleDebugMessage;
    }

    // legacy-compatible path
    public void Push(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        AddEntry(new EventLogEntry(
            DateTime.UtcNow,
            GameDebugChannel.General,
            GameDebugSeverity.Info,
            text
        ));
    }

    public string BuildMultilineText()
    {
        if (_entries.Count == 0)
            return "No events yet.";

        var sb = new StringBuilder(512);

        foreach (var entry in _entries)
        {
            sb.AppendLine(FormatEntry(entry));
        }

        return sb.ToString().TrimEnd();
    }

    private void HandleDebugMessage(GameDebugMessage msg)
    {
        if (!ShouldStore(msg))
            return;

        AddEntry(new EventLogEntry(
            DateTime.UtcNow,
            msg.Channel,
            msg.Severity,
            msg.Text,
            msg.VillagerId,
            msg.LocationId
        ));
    }

    private bool ShouldStore(GameDebugMessage msg)
    {
        // 1) Не зберігати дрібний шум
        if (msg.Channel == GameDebugChannel.Economy && msg.Severity == GameDebugSeverity.Info)
            return false;

        // 2) Не зберігати старт кожної дії
        if (msg.Text != null && msg.Text.Contains("started", StringComparison.OrdinalIgnoreCase))
            return false;

        // 3) Зберігати важливе
        switch (msg.Channel)
        {
            case GameDebugChannel.Task:
                return true;

            case GameDebugChannel.Explore:
                return true;

            case GameDebugChannel.Survey:
                return true;

            case GameDebugChannel.Location:
                return msg.Severity != GameDebugSeverity.Info;

            case GameDebugChannel.Economy:
                return msg.Severity != GameDebugSeverity.Info;

            case GameDebugChannel.Villager:
                return msg.Severity != GameDebugSeverity.Info;

            case GameDebugChannel.UI:
                return msg.Severity != GameDebugSeverity.Info;

            default:
                return msg.Severity != GameDebugSeverity.Info;
        }
    }

    private void AddEntry(EventLogEntry entry)
    {
        while (_entries.Count >= _capacity)
            _entries.Dequeue();

        _entries.Enqueue(entry);
        Changed?.Invoke();
    }

    private string FormatEntry(EventLogEntry entry)
    {
        string time = entry.TimeUtc.ToLocalTime().ToString("HH:mm");
        return $"[{time}] {entry.Text}";
    }
}