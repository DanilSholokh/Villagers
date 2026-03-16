using UnityEngine;

[CreateAssetMenu(
    fileName = "ExplorationUnlockRule",
    menuName = "Villagers/Explore/Exploration Unlock Rule"
)]
public class ExplorationUnlockRuleSO : ScriptableObject
{
    [SerializeField] private ExplorationUnlockRule data = new();

    public ExplorationUnlockRule Data => data;
}