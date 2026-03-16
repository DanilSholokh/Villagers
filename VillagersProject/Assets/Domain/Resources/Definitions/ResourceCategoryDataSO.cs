using UnityEngine;

[CreateAssetMenu(
    fileName = "ResourceCategoryData",
    menuName = "Villagers/Resources/Resource Category Data"
)]
public class ResourceCategoryDataSO : ScriptableObject
{
    [SerializeField] private ResourceCategoryData data = new();

    public ResourceCategoryData Data => data;
}