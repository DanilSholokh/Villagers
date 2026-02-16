using UnityEngine;

public class GatherLocationAuthoring : MonoBehaviour
{
    public string locationId;
    public string resourceId;   // "Fish", "Wood", "Stone"
    [Min(0f)] public float weight = 1f;
    public string displayName;
}
