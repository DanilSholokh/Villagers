using UnityEngine;


public class GameInstaller : MonoBehaviour
{
    public static TreasuryService Treasury { get; private set; }
    public static ExploreOutcomeService ExploreOutcome { get; private set; }
    public static TaskBoardService TaskBoard { get; private set; }
    public static ProgressionService Progression { get; private set; }
    public static ExploreSpotRegistry ExploreRegistry { get; private set; }
    public static VillagerRosterService Villagers { get; private set; }
    public static VillagerInventoryService Inventory { get; private set; }
    public static SelectedVillagerService SelectedVillager;
    public static CorpseService Corpses { get; private set; }
    public static LostService Lost { get; private set; }
    public static SettlementKnowledgeService Knowledge { get; private set; }
    public static LocationRegistry Locations { get; private set; }
    public static LocationService LocationService { get; private set; }
    public static SurveyOutcomeService SurveyOutcome { get; private set; }
    public static WorldDebugPopupService WorldDebugPopup { get; private set; }


    [SerializeField] private VillagerClickSelector villagerClickSelector; // якщо вже є в сцені

    [SerializeField] private TreasuryPanelView treasuryPanel;
    [SerializeField] private EventLogPanelView eventLogPanel;
    [SerializeField] private VillagerRosterPanelView villagerRosterPanel;
    [SerializeField] private TaskBoardUI taskBoardUI;
    [SerializeField] private WorldDebugPopupService worldDebugPopup;

    private EventLogService _log;

    private void Awake()
    {
        Debug.Log("[Installer] Awake INIT");

        // 1) Сервіси (дані/синглтони) — щоб UI в OnEnable вже їх бачив
        TaskBoard = new TaskBoardService();
        Treasury = new TreasuryService();
        ExploreOutcome = new ExploreOutcomeService();

        Progression = new ProgressionService();
        Villagers = new VillagerRosterService();
        _log = new EventLogService(10);
        Inventory = new VillagerInventoryService();
        SelectedVillager = new SelectedVillagerService();
        Knowledge = new SettlementKnowledgeService();
        SurveyOutcome = new SurveyOutcomeService();
        WorldDebugPopup = worldDebugPopup;

        // 2) Registry / world data
        ExploreRegistry = new ExploreSpotRegistry();
        ExploreRegistry.Initialize();

        Locations = new LocationRegistry();
        LocationService = new LocationService(Locations);
        LocationBootstrap.BuildFromScene(Locations);


        LocationService.DumpAllLocationsToLog();

        Corpses = new CorpseService();
        Lost = new LostService();


        // Важливо: тут НЕ стартуємо агентів і НЕ SetTasks
    }

    private void Start()
    {
        Debug.Log("[Installer] Start TRIGGERS");

        // 3) Bind UI (після Awake, але гарантовано один раз)
        if (treasuryPanel) treasuryPanel.Bind(Treasury);
        if (eventLogPanel) eventLogPanel.Bind(_log);
        if (villagerRosterPanel) villagerRosterPanel.Bind(Villagers, Progression);
        if (taskBoardUI) taskBoardUI.Bind(TaskBoard);
   

        // 4) Завантаження тасків
        var authoring = FindFirstObjectByType<TaskBoardAuthoring>();
        if (authoring == null)
        {
            Debug.LogError("[Installer] TaskBoardAuthoring not found in scene!");
            return;
        }
        TaskBoard.SetTasks(authoring.BuildRuntimeTasks());

        // 5) Запуск агентів (саме тут)
        var brains = FindObjectsByType<VillagerAgentBrain>(FindObjectsSortMode.None);
        Debug.Log($"[Installer] Starting agents={brains.Length}");

        foreach (var b in brains)
            b.Begin(TaskBoard, Treasury, _log, Villagers);

        Debug.Log("[Installer] Ready");
    }



}


