using UnityEngine;

public class LocationEntity
{
    public string spotId;
    public string displayName;
    public string gatherResourceId;

    public Vector3 position;
    public float weight;

    public bool discovered;

    // NEW
    public int dangerTier;

    // ✅ Старий конструктор лишається (backward-compatible)
    public LocationEntity(string spotId, string displayName, string gatherResourceId, Vector3 position, float weight)
        : this(spotId, displayName, gatherResourceId, position, weight, 0)
    {
    }

    // ✅ Новий — якщо хочеш задавати dangerTier явно
    public LocationEntity(string spotId, string displayName, string gatherResourceId, Vector3 position, float weight, int dangerTier)
    {
        this.spotId = spotId;
        this.displayName = displayName;
        this.gatherResourceId = gatherResourceId;
        this.position = position;
        this.weight = weight;

        this.dangerTier = Mathf.Clamp(dangerTier, 0, 5);
        this.discovered = false;
    }
}
