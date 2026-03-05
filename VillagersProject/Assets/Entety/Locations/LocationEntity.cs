using UnityEngine;

public class LocationEntity
{
    public string spotId;
    public string displayName;
    public string gatherResourceId;

    public Vector3 position;
    public float weight;


    public bool discovered;

    public LocationEntity(string spotId, string displayName, string gatherResourceId, Vector3 position, float weight)
    {
        this.spotId = spotId;
        this.displayName = displayName;
        this.gatherResourceId = gatherResourceId;
        this.position = position;
        this.weight = weight;
        this.discovered = false;
    }
}
