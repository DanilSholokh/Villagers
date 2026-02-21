using UnityEngine;


public class GameInstaller : MonoBehaviour
{

    public static TreasuryService Treasury { get; private set; }
    public static ExploreOutcomeService ExploreOutcome { get; private set; }
    public static TaskBoardService TaskBoard { get; private set; }



    public static ExploreSpotRegistry ExploreRegistry { get; private set; }



    [SerializeField] private TreasuryPanelView treasuryPanel;
    [SerializeField] private EventLogPanelView eventLogPanel;


    private EventLogService _log;

    private void Start()
    {
        Debug.Log("[Installer] Boot");

        ExploreRegistry = new ExploreSpotRegistry();
        ExploreRegistry.Initialize();

        for (int i = 0; i < 10; i++)
        {
            var s = ExploreRegistry.GetRandomSpotWeighted();
            Debug.Log($"[TEST] Picked spot={s.spotId} w={s.weight}");
        }


        ExploreOutcome = new ExploreOutcomeService();

        // TaskBoard 
        TaskBoard = new TaskBoardService();

        // Treasury
        Treasury = new TreasuryService();
        if (treasuryPanel) treasuryPanel.Bind(Treasury);


        // Debug log
        _log = new EventLogService(10);
        if (eventLogPanel) eventLogPanel.Bind(_log);

        _log.Push("[Installer] Ready");



        var authoring = FindFirstObjectByType<TaskBoardAuthoring>();
        if (authoring == null)
        {
            Debug.LogError("[Installer] TaskBoardAuthoring not found in scene!");
        }
        else
        {
            TaskBoard.SetTasks(authoring.BuildRuntimeTasks());


            var brains = FindObjectsByType<VillagerAgentBrain>(FindObjectsSortMode.None);
            Debug.Log($"[Installer] Starting agents={brains.Length}");

            foreach (var b in brains)
                b.Begin(TaskBoard, ExploreRegistry, Treasury, _log);

            Debug.Log("[Installer] Ready");


        }





    }
}

