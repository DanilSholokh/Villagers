using System.Collections.Generic;
using UnityEngine;

public class WorldDebugPopupService : MonoBehaviour
{
    [SerializeField] private WorldDebugPopupView popupPrefab;
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private int initialPoolSize = 12;

    [Header("Channel filters")]
    [SerializeField] private bool showEconomy = true;
    [SerializeField] private bool showExplore = true;
    [SerializeField] private bool showSurvey = true;
    [SerializeField] private bool showTaskWarnings = true;

    private readonly List<WorldDebugPopupView> _pool = new();

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        Warmup();
    }

    private void OnEnable()
    {
        GameDebug.OnMessage += HandleMessage;
    }

    private void OnDisable()
    {
        GameDebug.OnMessage -= HandleMessage;
    }

    private void Warmup()
    {
        if (popupPrefab == null || popupRoot == null)
            return;

        for (int i = 0; i < initialPoolSize; i++)
        {
            var popup = Instantiate(popupPrefab, popupRoot);
            popup.gameObject.SetActive(false);
            _pool.Add(popup);
        }
    }

    private void HandleMessage(GameDebugMessage msg)
    {
        if (!msg.WorldPosition.HasValue)
            return;

        if (!ShouldShow(msg))
            return;

        var popup = GetFreePopup();
        if (popup == null)
            return;

        popup.Show(
            worldCamera != null ? worldCamera : Camera.main,
            msg.WorldPosition.Value,
            msg.Text,
            ResolveColor(msg),
            msg.Lifetime
        );
    }

    private bool ShouldShow(GameDebugMessage msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Text))
            return false;

        // не показуємо "start" popup-и
        if (msg.Text == "Gather" || msg.Text == "Explore" || msg.Text == "Survey")
            return false;

        switch (msg.Channel)
        {
            case GameDebugChannel.Economy:
                return showEconomy;

            case GameDebugChannel.Explore:
                return showExplore;

            case GameDebugChannel.Survey:
                return showSurvey;

            case GameDebugChannel.Task:
                return showTaskWarnings && msg.Severity != GameDebugSeverity.Info;

            default:
                return false;
        }
    }

    private Color ResolveColor(GameDebugMessage msg)
    {
        switch (msg.Severity)
        {
            case GameDebugSeverity.Error:
                return new Color(1f, 0.35f, 0.35f, 1f);

            case GameDebugSeverity.Warning:
                return new Color(1f, 0.82f, 0.25f, 1f);

            default:
                return msg.Channel switch
                {
                    GameDebugChannel.Economy => new Color(0.45f, 1f, 0.45f, 1f),
                    GameDebugChannel.Explore => new Color(0.55f, 0.95f, 1f, 1f),
                    GameDebugChannel.Survey => new Color(0.95f, 0.95f, 1f, 1f),
                    _ => Color.white
                };
        }
    }

    private WorldDebugPopupView GetFreePopup()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (!_pool[i].gameObject.activeSelf)
                return _pool[i];
        }

        if (popupPrefab == null || popupRoot == null)
            return null;

        var popup = Instantiate(popupPrefab, popupRoot);
        popup.gameObject.SetActive(false);
        _pool.Add(popup);
        return popup;
    }
}