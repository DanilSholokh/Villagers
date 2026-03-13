using TMPro;
using UnityEngine;

public class EventLogPanelView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI logText;

    private EventLogService _log;

    public void Bind(EventLogService log)
    {
        if (_log != null)
            _log.Changed -= Refresh;

        _log = log;

        if (_log != null)
            _log.Changed += Refresh;

        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (_log != null)
            _log.Changed -= Refresh;
    }

    private void OnDestroy()
    {
        if (_log != null)
            _log.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (logText == null)
            return;

        if (_log == null)
        {
            logText.text = "Event log not bound.";
            return;
        }

        logText.text = _log.BuildMultilineText();
    }
}