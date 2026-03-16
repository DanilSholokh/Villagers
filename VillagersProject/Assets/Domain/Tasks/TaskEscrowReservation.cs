using System;

[Serializable]
public class TaskEscrowReservation
{
    public string taskId;
    public string agentId;

    public bool usesBundleCost;
    public bool usesLegacyGold;

    public int lockedGold;
    public ResourceBundle spentBundle = new();

    public bool HasAnyReservation
    {
        get
        {
            bool hasGold = usesLegacyGold && lockedGold > 0;
            bool hasBundle = usesBundleCost && spentBundle != null && !spentBundle.IsEmpty;
            return hasGold || hasBundle;
        }
    }

    public void Clear()
    {
        taskId = string.Empty;
        agentId = string.Empty;
        usesBundleCost = false;
        usesLegacyGold = false;
        lockedGold = 0;
        spentBundle = new ResourceBundle();
    }
}