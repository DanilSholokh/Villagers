using System.Collections.Generic;
using UnityEngine;

public class GameInstaller : MonoBehaviour
{
    public static TreasuryService Treasury { get; private set; }
    public static ExploreOutcomeService ExploreOutcome { get; private set; }
    public static TaskBoardService TaskBoard { get; private set; }
    public static ProgressionService Progression { get; private set; }
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
    public static SelectedLocationService SelectedLocation { get; private set; }
    public static ResourceRegistry Resources { get; private set; }
    public static ResourceCategoryRegistry ResourceCategories { get; private set; }
    public static ResourceCategoryService ResourceCategoryService { get; private set; }
    public static CategoryValueService CategoryValues { get; private set; }
    public static CategoryQueryService CategoryQueries { get; private set; }
    public static EconomicSelectionService EconomicSelection { get; private set; }
    public static EconomicValidationService EconomicValidation { get; private set; }
    public static EconomicExecutionService EconomicExecution { get; private set; }
    public static EconomicSimulationService EconomicSimulation { get; private set; }
    public static EconomicSellService EconomicSell { get; private set; }
    public static ExplorationUnlockService ExplorationUnlock { get; private set; }

    [SerializeField] private VillagerClickSelector villagerClickSelector;
    [SerializeField] private TreasuryPanelView treasuryPanel;
    [SerializeField] private EventLogPanelView eventLogPanel;
    [SerializeField] private VillagerRosterPanelView villagerRosterPanel;
    [SerializeField] private TaskBoardUI taskBoardUI;
    [SerializeField] private WorldDebugPopupService worldDebugPopup;
    [SerializeField] private LocationMetricsPanelView locationMetricsPanel;
    [SerializeField] private ResourceDataSO[] resourceAssets;
    [SerializeField] private ResourceCategoryDataSO[] resourceCategoryAssets;
    [SerializeField] private ExplorationUnlockRuleSO explorationUnlockRuleAsset;
    [SerializeField] private int startingGold = 25;

    private EventLogService _log;

    private void Awake()
    {
        GameDebug.Info(GameDebugChannel.General, "Installer ready");

        _log = new EventLogService(10);

        InitializeCoreServices();
        InitializeEconomy();
        InitializeWorld();
        RunBootstrapValidation();
        EmitBootstrapSummary();
    }

    private void Start()
    {
        if (treasuryPanel) treasuryPanel.Bind(Treasury);
        if (eventLogPanel) eventLogPanel.Bind(_log);
        if (villagerRosterPanel) villagerRosterPanel.Bind(Villagers, Progression);
        if (taskBoardUI) taskBoardUI.Bind(TaskBoard);

        if (locationMetricsPanel != null)
            locationMetricsPanel.Bind(LocationService, SelectedLocation);

        Treasury.InitializeGold(startingGold);

        var authoring = FindFirstObjectByType<TaskBoardAuthoring>();
        if (authoring != null)
        {
            ValidateTaskBoardAuthoring(authoring);
            TaskBoard.SetTasks(authoring.BuildRuntimeTasks());
        }
        else
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                "TaskBoardAuthoring not found in scene. Board starts empty."
            );
        }

        ValidateTaskPublisherPanels();

        var brains = FindObjectsByType<VillagerAgentBrain>(FindObjectsSortMode.None);
        foreach (var b in brains)
            b.Begin(TaskBoard, Treasury, _log, Villagers);

        GameDebug.Info(
            GameDebugChannel.General,
            $"Villager brains started: {brains.Length}"
        );
    }

    private void OnDestroy()
    {
        _log?.Dispose();
    }

    private void InitializeCoreServices()
    {
        Treasury = new TreasuryService();
        ExploreOutcome = new ExploreOutcomeService();
        TaskBoard = new TaskBoardService();
        Progression = new ProgressionService();
        Villagers = new VillagerRosterService();
        Inventory = new VillagerInventoryService();
        SelectedVillager = new SelectedVillagerService();
        Knowledge = new SettlementKnowledgeService();
        SurveyOutcome = new SurveyOutcomeService();
        SelectedLocation = new SelectedLocationService();
        WorldDebugPopup = worldDebugPopup;
        Corpses = new CorpseService();
        Lost = new LostService();
    }

    private void InitializeEconomy()
    {
        Resources = new ResourceRegistry();
        Resources.LoadFromAssets(resourceAssets);

        GameDebug.Info(
            GameDebugChannel.General,
            $"ResourceRegistry loaded: {Resources.Count} resources"
        );

        ResourceCategories = new ResourceCategoryRegistry();
        ResourceCategories.LoadFromAssets(resourceCategoryAssets);

        GameDebug.Info(
            GameDebugChannel.General,
            $"ResourceCategoryRegistry loaded: {ResourceCategories.Count} categories"
        );

        ResourceCategoryService = new ResourceCategoryService(ResourceCategories);
        CategoryValues = new CategoryValueService();
        CategoryQueries = new CategoryQueryService(CategoryValues);
        EconomicSelection = new EconomicSelectionService(CategoryValues, CategoryQueries);
        EconomicValidation = new EconomicValidationService(
            CategoryValues,
            CategoryQueries,
            EconomicSelection
        );
        EconomicExecution = new EconomicExecutionService(EconomicValidation);
        EconomicSimulation = new EconomicSimulationService(EconomicValidation);
        EconomicSell = new EconomicSellService();

        ExplorationUnlock = new ExplorationUnlockService(
            explorationUnlockRuleAsset != null ? explorationUnlockRuleAsset.Data : null
        );

        GameDebug.Info(GameDebugChannel.General, "Category value layer ready");
        GameDebug.Info(GameDebugChannel.General, "Economic recipe layer ready");
        GameDebug.Info(GameDebugChannel.General, "Economic sell layer ready");
    }

    private void InitializeWorld()
    {
        Locations = new LocationRegistry();
        LocationService = new LocationService(Locations);
        LocationBootstrap.BuildFromScene(Locations);

        LocationService.DumpAllLocationsToLog();
    }

    private void RunBootstrapValidation()
    {
        ValidateResources();
        ValidateResourceCategories();
        ValidateExplorationUnlockRule();
        ValidateSceneSpots();
        ValidateSceneBindings();
    }

    private void ValidateResources()
    {
        if (resourceAssets == null || resourceAssets.Length == 0)
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                "No ResourceDataSO assets assigned in GameInstaller."
            );
            return;
        }

        var seen = new HashSet<string>();

        for (int i = 0; i < resourceAssets.Length; i++)
        {
            var asset = resourceAssets[i];
            if (asset == null)
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"ResourceDataSO entry at index {i} is null."
                );
                continue;
            }

            var data = asset.Data;
            if (data == null)
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"ResourceDataSO '{asset.name}' has null Data."
                );
                continue;
            }

            string id = Normalize(data.resourceId);
            if (string.IsNullOrWhiteSpace(id))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"ResourceDataSO '{asset.name}' has empty resourceId."
                );
                continue;
            }

            if (!seen.Add(id))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"Duplicate resourceId in GameInstaller assets: '{id}'."
                );
            }

            if (data.baseValue < 0)
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"Resource '{id}' has negative baseValue={data.baseValue}. Runtime clamps it to 0."
                );
            }

            if (string.IsNullOrWhiteSpace(data.categoryId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"Resource '{id}' has empty categoryId."
                );
            }
        }
    }

    private void ValidateResourceCategories()
    {
        if (resourceCategoryAssets == null || resourceCategoryAssets.Length == 0)
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                "No ResourceCategoryDataSO assets assigned in GameInstaller."
            );
        }
        else
        {
            var seen = new HashSet<string>();

            for (int i = 0; i < resourceCategoryAssets.Length; i++)
            {
                var asset = resourceCategoryAssets[i];
                if (asset == null)
                {
                    GameDebug.Warning(
                        GameDebugChannel.General,
                        $"ResourceCategoryDataSO entry at index {i} is null."
                    );
                    continue;
                }

                var data = asset.Data;
                if (data == null)
                {
                    GameDebug.Warning(
                        GameDebugChannel.General,
                        $"ResourceCategoryDataSO '{asset.name}' has null Data."
                    );
                    continue;
                }

                string id = Normalize(data.categoryId);
                if (string.IsNullOrWhiteSpace(id))
                {
                    GameDebug.Warning(
                        GameDebugChannel.General,
                        $"ResourceCategoryDataSO '{asset.name}' has empty categoryId."
                    );
                    continue;
                }

                if (!seen.Add(id))
                {
                    GameDebug.Warning(
                        GameDebugChannel.General,
                        $"Duplicate categoryId in GameInstaller assets: '{id}'."
                    );
                }

                if (data.tradeMultiplier < 0f)
                {
                    GameDebug.Warning(
                        GameDebugChannel.General,
                        $"Category '{id}' has negative tradeMultiplier={data.tradeMultiplier}. Runtime clamps it to 0."
                    );
                }
            }
        }

        if (Resources == null || ResourceCategories == null)
            return;

        foreach (var resource in Resources.GetAll())
        {
            if (resource == null)
                continue;

            if (string.IsNullOrWhiteSpace(resource.categoryId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"Resource '{resource.resourceId}' has empty categoryId"
                );
                continue;
            }

            if (!ResourceCategories.Has(resource.categoryId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"Resource '{resource.resourceId}' references missing category '{resource.categoryId}'"
                );
            }
        }
    }

    private void ValidateExplorationUnlockRule()
    {
        if (explorationUnlockRuleAsset == null)
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                "ExplorationUnlockRuleSO is not assigned. Runtime will use fallback default rule."
            );
            return;
        }

        var rule = explorationUnlockRuleAsset.Data;
        if (rule == null)
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                "ExplorationUnlockRuleSO has null Data. Runtime will use fallback default rule."
            );
            return;
        }

        string categoryId = Normalize(rule.categoryId);
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                "ExplorationUnlockRule has empty categoryId."
            );
        }
        else if (ResourceCategories != null && !ResourceCategories.Has(categoryId))
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                $"ExplorationUnlockRule references missing category '{categoryId}'."
            );
        }

        if (rule.requiredValue < 0)
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                $"ExplorationUnlockRule has negative requiredValue={rule.requiredValue}. Runtime clamps it to 0."
            );
        }

        GameDebug.Info(
            GameDebugChannel.General,
            $"Exploration unlock rule: id='{Safe(rule.ruleId, "discover_region")}', category='{Safe(categoryId, "(empty)")}', requiredValue={Mathf.Max(0, rule.requiredValue)}"
        );
    }

    private void ValidateSceneSpots()
    {
        var spots = FindObjectsByType<ExploreSpotAuthoring>(FindObjectsSortMode.None);
        if (spots == null || spots.Length == 0)
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                "No ExploreSpotAuthoring objects found in scene."
            );
            return;
        }

        var seenSpotIds = new HashSet<string>();

        for (int i = 0; i < spots.Length; i++)
        {
            var spot = spots[i];
            if (spot == null)
                continue;

            string spotId = Normalize(spot.spotId);
            if (string.IsNullOrWhiteSpace(spotId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"ExploreSpotAuthoring '{spot.name}' has empty spotId."
                );
                continue;
            }

            if (!seenSpotIds.Add(spotId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"Duplicate ExploreSpotAuthoring spotId in scene: '{spotId}'."
                );
            }

            string gatherId = Normalize(spot.gatherResourceId);
            if (!string.IsNullOrWhiteSpace(gatherId) && (Resources == null || !Resources.Has(gatherId)))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"Spot '{spotId}' references missing gatherResourceId '{gatherId}'."
                );
            }

            if (spot.potentialResourceIds == null)
                continue;

            for (int r = 0; r < spot.potentialResourceIds.Length; r++)
            {
                string potentialId = Normalize(spot.potentialResourceIds[r]);
                if (string.IsNullOrWhiteSpace(potentialId))
                    continue;

                if (Resources == null || !Resources.Has(potentialId))
                {
                    GameDebug.Warning(
                        GameDebugChannel.General,
                        $"Spot '{spotId}' references missing potentialResourceId '{potentialId}'."
                    );
                }
            }
        }
    }

    private void ValidateSceneBindings()
    {
        if (treasuryPanel == null)
            GameDebug.Warning(GameDebugChannel.General, "TreasuryPanelView is not assigned in GameInstaller.");

        if (eventLogPanel == null)
            GameDebug.Warning(GameDebugChannel.General, "EventLogPanelView is not assigned in GameInstaller.");

        if (villagerRosterPanel == null)
            GameDebug.Warning(GameDebugChannel.General, "VillagerRosterPanelView is not assigned in GameInstaller.");

        if (taskBoardUI == null)
            GameDebug.Warning(GameDebugChannel.General, "TaskBoardUI is not assigned in GameInstaller.");

        if (locationMetricsPanel == null)
            GameDebug.Warning(GameDebugChannel.General, "LocationMetricsPanelView is not assigned in GameInstaller.");

        if (worldDebugPopup == null)
            GameDebug.Warning(GameDebugChannel.General, "WorldDebugPopupService is not assigned in GameInstaller.");

        if (villagerClickSelector == null)
            GameDebug.Warning(GameDebugChannel.General, "VillagerClickSelector is not assigned in GameInstaller.");
    }

    private void ValidateTaskBoardAuthoring(TaskBoardAuthoring authoring)
    {
        if (authoring == null)
            return;

        if (authoring.tasks == null || authoring.tasks.Count == 0)
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                "TaskBoardAuthoring found, but it contains no task definitions."
            );
            return;
        }

        var seenTaskIds = new HashSet<string>();

        for (int i = 0; i < authoring.tasks.Count; i++)
        {
            var def = authoring.tasks[i];
            if (def == null)
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskBoardAuthoring task entry at index {i} is null."
                );
                continue;
            }

            string taskId = Normalize(def.taskId);
            if (string.IsNullOrWhiteSpace(taskId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskBoardAuthoring task at index {i} has empty taskId."
                );
            }
            else if (!seenTaskIds.Add(taskId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"Duplicate TaskBoardAuthoring taskId '{taskId}'."
                );
            }

            string resourceId = Normalize(def.resourceId);
            if (def.type == TaskType.Gather && string.IsNullOrWhiteSpace(resourceId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskBoardAuthoring gather task '{Safe(taskId, $"index_{i}")}' has empty resourceId."
                );
            }

            if (!string.IsNullOrWhiteSpace(resourceId) && Resources != null && !Resources.Has(resourceId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskBoardAuthoring task '{Safe(taskId, $"index_{i}")}' references missing resourceId '{resourceId}'."
                );
            }

            string rewardId = Normalize(def.rewardResourceId);
            if (!string.IsNullOrWhiteSpace(rewardId) && Resources != null && !Resources.Has(rewardId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskBoardAuthoring task '{Safe(taskId, $"index_{i}")}' references missing rewardResourceId '{rewardId}'."
                );
            }

            string costId = Normalize(def.upfrontCostResourceId);
            if (!string.IsNullOrWhiteSpace(costId) && Resources != null && !Resources.Has(costId))
            {
                GameDebug.Warning(
                    GameDebugChannel.General,
                    $"TaskBoardAuthoring task '{Safe(taskId, $"index_{i}")}' references missing upfrontCostResourceId '{costId}'."
                );
            }
        }
    }

    private void ValidateTaskPublisherPanels()
    {
        var panels = FindObjectsByType<TaskPublisherPanel>(FindObjectsSortMode.None);
        if (panels == null || panels.Length == 0)
        {
            GameDebug.Warning(
                GameDebugChannel.General,
                "No TaskPublisherPanel objects found in scene."
            );
            return;
        }

        for (int i = 0; i < panels.Length; i++)
            panels[i].ValidateBootstrapConfig();
    }

    private void EmitBootstrapSummary()
    {
        int locationCount = Locations != null ? CountLocations(Locations.GetAll()) : 0;
        int unknownCount = 0;
        int discoveredCount = 0;

        if (Locations != null)
        {
            foreach (var loc in Locations.GetAll())
            {
                if (loc == null)
                    continue;

                if (loc.status == LocationStatus.Unknown)
                    unknownCount++;
                else if (loc.status == LocationStatus.Discovered)
                    discoveredCount++;
            }
        }

        var authoring = FindFirstObjectByType<TaskBoardAuthoring>();
        var publishers = FindObjectsByType<TaskPublisherPanel>(FindObjectsSortMode.None);

        GameDebug.Info(
            GameDebugChannel.General,
            $"Bootstrap summary | resources={SafeCount(Resources)} categories={SafeCount(ResourceCategories)} locations={locationCount} discovered={discoveredCount} unknown={unknownCount} explorationRuleAsset={(explorationUnlockRuleAsset != null ? "YES" : "NO")} taskBoardAuthoring={(authoring != null ? "YES" : "NO")} taskPublishers={(publishers != null ? publishers.Length : 0)}"
        );
    }

    private static int SafeCount(ResourceRegistry registry)
        => registry != null ? registry.Count : 0;

    private static int SafeCount(ResourceCategoryRegistry registry)
        => registry != null ? registry.Count : 0;

    private static int CountLocations(IReadOnlyCollection<LocationModel> locations)
    {
        if (locations == null)
            return 0;

        int count = 0;
        foreach (var _ in locations)
            count++;

        return count;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}