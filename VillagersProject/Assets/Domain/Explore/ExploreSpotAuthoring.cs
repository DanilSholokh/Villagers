using UnityEngine;

public class ExploreSpotAuthoring : MonoBehaviour
{


    public string[] potentialResourceIds;

    public string spotId;
    public float weight = 1f;
    public string displayName;


    // NEW
    [Range(0, 5)]
    public int dangerTier = 0;

    public string gatherResourceId;

}

