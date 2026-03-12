using System;

[Serializable]
public class LocationResource
{
    public string resourceId;
    public float timePerUnit = 1f;
    public int difficulty = 0;
    public bool isUnlocked = true;

}