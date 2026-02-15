using UnityEngine;


public class GameInstaller : MonoBehaviour
{

    public static TreasuryService Treasury { get; private set; }
    public static ExploreOutcomeService ExploreOutcome { get; private set; }



    public static ExploreSpotRegistry ExploreRegistry { get; private set; }

    private void Awake()
    {
        Debug.Log("[Installer] Boot");

        ExploreRegistry = new ExploreSpotRegistry();
        ExploreRegistry.Initialize();

        for (int i = 0; i < 10; i++)
        {
            var s = ExploreRegistry.GetRandomSpotWeighted();
            Debug.Log($"[TEST] Picked spot={s.spotId} w={s.weight}");
        }


        Treasury = new TreasuryService();
        ExploreOutcome = new ExploreOutcomeService();



    }
}

