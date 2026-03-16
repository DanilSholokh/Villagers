using UnityEngine;

[CreateAssetMenu(
    fileName = "ResourceData",
    menuName = "Villagers/Resources/Resource Data"
)]
public class ResourceDataSO : ScriptableObject
{
    [SerializeField] private ResourceData data = new();

    public ResourceData Data => data;
}