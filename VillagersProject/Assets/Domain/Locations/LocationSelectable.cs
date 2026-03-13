using UnityEngine;

public class LocationSelectable : MonoBehaviour
{
    [SerializeField] private string locationId;

    public string LocationId => locationId;

    public void SetLocationId(string id)
    {
        locationId = id;
    }
}