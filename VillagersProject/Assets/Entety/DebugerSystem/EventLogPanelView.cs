using System.Text;
using TMPro;
using UnityEngine;

public class EventLogPanelView : MonoBehaviour
{
    public TextMeshProUGUI logText;

    private EventLogService _log;

    public void Bind(EventLogService log)
    {
        if (_log != null)
            _log.OnPushed -= HandlePushed;

        _log = log;

        if (_log != null)
            _log.OnPushed += HandlePushed;

        Rebuild();
    }


    private void OnEnable()
    {
        if (_log != null)
        {
            _log.OnPushed -= HandlePushed; // safety від дублю
            _log.OnPushed += HandlePushed;
        }

        Rebuild();
    }

    private void OnDisable()
    {
        if (_log != null)
            _log.OnPushed -= HandlePushed;
    }

    private void HandlePushed(string _)
    {
        Rebuild();
    }

    private void Rebuild()
    {
        if (logText == null) return;
        if (_log == null)
        {
            logText.text = "";
            return;
        }

        var sb = new StringBuilder(256);
        foreach (var l in _log.Lines)
            sb.AppendLine(l);

        logText.text = sb.ToString();
    }
}
