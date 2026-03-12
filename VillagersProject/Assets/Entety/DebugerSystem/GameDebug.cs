using System;
using UnityEngine;

public static class GameDebug
{
    public static bool EnableConsoleLogs = true;

    public static bool EnableUIChannel = true;
    public static bool EnableTaskChannel = true;
    public static bool EnableVillagerChannel = true;
    public static bool EnableLocationChannel = true;
    public static bool EnableExploreChannel = true;
    public static bool EnableSurveyChannel = true;
    public static bool EnableEconomyChannel = true;
    public static bool EnableGeneralChannel = true;

    public static event Action<GameDebugMessage> OnMessage;

    public static void Info(GameDebugChannel channel, string text,
        Vector3? worldPosition = null,
        string locationId = null,
        string villagerId = null,
        float lifetime = 1.75f)
    {
        Publish(new GameDebugMessage(channel, GameDebugSeverity.Info, text, worldPosition, locationId, villagerId, lifetime));
    }

    public static void Warning(GameDebugChannel channel, string text,
        Vector3? worldPosition = null,
        string locationId = null,
        string villagerId = null,
        float lifetime = 2.0f)
    {
        Publish(new GameDebugMessage(channel, GameDebugSeverity.Warning, text, worldPosition, locationId, villagerId, lifetime));
    }

    public static void Error(GameDebugChannel channel, string text,
        Vector3? worldPosition = null,
        string locationId = null,
        string villagerId = null,
        float lifetime = 2.5f)
    {
        Publish(new GameDebugMessage(channel, GameDebugSeverity.Error, text, worldPosition, locationId, villagerId, lifetime));
    }

    public static void Publish(GameDebugMessage msg)
    {
        if (!IsChannelEnabled(msg.Channel))
            return;

        if (EnableConsoleLogs)
        {
            string prefix = $"[DBG][{msg.Channel}][{msg.Severity}] ";

            switch (msg.Severity)
            {
                case GameDebugSeverity.Error:
                    Debug.LogError(prefix + msg.Text);
                    break;
                case GameDebugSeverity.Warning:
                    Debug.LogWarning(prefix + msg.Text);
                    break;
                default:
                    Debug.Log(prefix + msg.Text);
                    break;
            }
        }

        OnMessage?.Invoke(msg);
    }

    private static bool IsChannelEnabled(GameDebugChannel channel)
    {
        return channel switch
        {
            GameDebugChannel.UI => EnableUIChannel,
            GameDebugChannel.Task => EnableTaskChannel,
            GameDebugChannel.Villager => EnableVillagerChannel,
            GameDebugChannel.Location => EnableLocationChannel,
            GameDebugChannel.Explore => EnableExploreChannel,
            GameDebugChannel.Survey => EnableSurveyChannel,
            GameDebugChannel.Economy => EnableEconomyChannel,
            _ => EnableGeneralChannel
        };
    }
}