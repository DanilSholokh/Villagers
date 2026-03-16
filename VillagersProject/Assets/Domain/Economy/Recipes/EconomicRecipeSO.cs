using UnityEngine;

[CreateAssetMenu(
    fileName = "EconomicRecipe",
    menuName = "Villagers/Economy/Economic Recipe"
)]
public class EconomicRecipeSO : ScriptableObject
{
    [SerializeField] private EconomicRecipe recipe = new();

    public EconomicRecipe Recipe => recipe;
}