using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class VillagerAgentBrain : MonoBehaviour
{
    [SerializeField] private string agentId = "A";
    [SerializeField] private Transform homePoint;

    [Header("Brain tuning")]
    [SerializeField] private float noTaskDelay = 0.5f;
    [SerializeField] private float afterCycleDelay = 0.25f;
    [SerializeField] private float gatherNoSpotDelay = 0.25f;

    [Header("NavMesh safety")]
    [SerializeField] private float arriveTimeoutSec = 20f; // щоб не зависати назавжди
    [SerializeField] private float minVelocitySqr = 0.01f; // стабілізація "приїхав"

    [SerializeField] private float arriveDistance = 1.6f;


    // -------------------- CARGO (prototype) --------------------
    [SerializeField] private VillagerCargo _cargo = new VillagerCargo();

    private int _lastWorkLocationDangerTier;

    private NavMeshAgent agent;
    private Coroutine loop;

    private bool started;
    private bool _completedThisCycle;


    private TaskBoardService _board;

    private TreasuryService _treasury;
    private EventLogService _log;

    private Vector3 _lastWorkPos;
    private string _lastWorkLocationId;

    private VillagerRosterService _roster;

    public string AgentId => agentId;

    public System.Collections.Generic.Dictionary<string, int> GetCargoSnapshot()
    {
        return _cargo != null
            ? _cargo.Snapshot()
            : new System.Collections.Generic.Dictionary<string, int>();
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // ❗ ВАЖЛИВО: OnEnable/Start НЕ запускають логіку

    public void Begin(TaskBoardService board, TreasuryService treasury, EventLogService log, VillagerRosterService roster)
    {
        _board = board;

        _treasury = treasury;
        _log = log;
        _roster = roster;

        _roster?.GetOrCreate(agentId, displayName: agentId);
        _roster?.SetStatus(agentId, VillagerStatus.Idle);

        GameInstaller.Progression?.EnsureInitialized(agentId, 1, 1, 1);

        if (started) return;
        started = true;


        if (homePoint == null)
        {
            Debug.LogError($"[VillagerAgentBrain] homePoint is not set! agent={agentId}");
            started = false;
            return;
        }

        if (_board == null)
        {
            Debug.LogError($"[VillagerAgentBrain] TaskBoard is null at Begin! agent={agentId}");
            started = false;
            return;
        }

        if (agent == null)
        {
            Debug.LogError($"[VillagerAgentBrain] NavMeshAgent missing! agent={agentId}");
            started = false;
            return;
        }

        loop = StartCoroutine(Run());
    }

    public void StopBrain()
    {
        if (loop != null)
            StopCoroutine(loop);

        loop = null;
        started = false;
    }

    private IEnumerator Run()
    {
        while (true)
        {
            _roster?.SetStatus(agentId, VillagerStatus.LookingForTask);

            // 1) Pick
            var task = TaskSelectionLogic.PickBest(_board);
            if (task == null)
            {
                yield return new WaitForSeconds(noTaskDelay);
                continue;
            }

            // 2) Reserve
            if (!_board.TryReserve(task.taskId, agentId))
            {
                yield return null;
                continue;
            }

            // ✅ Escrow hold (wage)
            if (_treasury != null && !_treasury.TryHoldGold(task.wageGold))
            {
                // якщо грошей нема — відпускаємо слот назад
                _board.Release(task.taskId, agentId);
                Log($"Reserve failed: not enough gold for wage={task.wageGold}");
                yield return null;
                continue;
            }

            Log($"Picked task={task.taskId} ({task.displayName}) wage={task.wageGold}");
            _roster?.SetStatus(agentId, VillagerStatus.ReservedTask, task.taskId, task.displayName);

            // 3) Execute
            _completedThisCycle = false;
            _roster?.SetStatus(agentId, VillagerStatus.MovingToTarget, task.taskId, task.displayName);
            PublishTaskEvent($"Task started: {task.taskId} ({task.type})");

            if (task.type == TaskType.Gather)
                yield return DoGather(task);
            else if (task.type == TaskType.ExploreNewLocation)
                yield return DoExploreNewLocation(task);
            else if (task.type == TaskType.SurveyKnownLocation)
                yield return DoSurveyKnownLocation(task);

            // якщо action already succeeded і сам виставив _completedThisCycle=true,
            // не можна після цього ще раз кидати fail/lost/death resolution
            if (_completedThisCycle)
            {
                _roster?.SetStatus(agentId, VillagerStatus.ReturningHome, task.taskId, task.displayName);
                yield return DoGoHome();

                FinalizeTask(task, true);

                _roster?.SetStatus(agentId, VillagerStatus.Idle);
                yield return new WaitForSeconds(afterCycleDelay);
                continue;
            }

            // location danger modifier (MVP simple):
            // +3% fail chance per danger tier (0..5) => +0..15%
            int locDanger = GetTaskLocationDanger(task);
            float locationFailAdd = 0.03f * locDanger;

            float failChance = Mathf.Clamp01(task.baseFailChance + locationFailAdd);
            float roll = UnityEngine.Random.value;
            bool failed = roll < failChance;

            Log($"FailRoll task={task.taskId} baseFail={task.baseFailChance:F2} " +
                $"locDanger={locDanger} (+{locationFailAdd:F2}) " +
                $"failChance={failChance:F2} roll={roll:F3} failed={failed}");

            if (failed)
            {
                int effectiveRiskTier = Mathf.Clamp(task.riskTier + locDanger, 0, 5);
                var penalty = PenaltyRoll.RollPenalty(effectiveRiskTier);

                Log($"PenaltyRoll riskTier={task.riskTier} + locDanger={locDanger} => {effectiveRiskTier} result={penalty}");

                if (penalty == PenaltyType.Death)
                {
                    Log($"Villager died on task {task.taskId}");
                    PublishTaskEvent("Dead", GameDebugSeverity.Error);
                    HandleDeath(task);
                    yield break;
                }

                if (penalty == PenaltyType.Lost)
                {
                    PublishTaskEvent($"Villager lost on task {task.taskId}", GameDebugSeverity.Warning);
                    HandleLost(task);
                    yield break;
                }

                _completedThisCycle = false;

                _roster?.SetStatus(agentId, VillagerStatus.ReturningHome, task.taskId, "FAILED");
                yield return DoGoHome();

                _cargo.Clear();

                Log($"Villager lost on task {task.taskId}");
                PublishTaskEvent("Lost", GameDebugSeverity.Warning);

                Log($"Task failed roll: {task.taskId} (risk={task.riskTier}, danger={locDanger})");
                PublishTaskEvent("Failed", GameDebugSeverity.Warning);

                FinalizeTask(task, false);

                _roster?.SetStatus(agentId, VillagerStatus.Idle);
                yield return new WaitForSeconds(afterCycleDelay);
                continue;
            }

            // 4) Success path: return home + finalize
            _roster?.SetStatus(agentId, VillagerStatus.ReturningHome, task.taskId, task.displayName);
            yield return DoGoHome();

            PublishTaskEvent($"Task completed: {task.taskId}");
            FinalizeTask(task, _completedThisCycle);

            _roster?.SetStatus(agentId, VillagerStatus.Idle);
            yield return new WaitForSeconds(afterCycleDelay);
        }
    }

    // -------------------- ACTIONS --------------------

    private IEnumerator DoExploreNewLocation(TaskInstance task)
    {
        _completedThisCycle = false;

        var locations = GameInstaller.LocationService;
        if (locations == null)
        {
            PublishExploreEvent("Explore failed: LocationService is null", GameDebugSeverity.Error); ;
            _completedThisCycle = false;
            yield break;
        }

        string locationId = task.targetLocationId;
        LocationModel loc = null;

        if (!string.IsNullOrWhiteSpace(locationId))
        {
            loc = locations.GetLocation(locationId);
        }
        else
        {
            locationId = locations.FindRandomUnknownLocationId();
            if (!string.IsNullOrWhiteSpace(locationId))
                loc = locations.GetLocation(locationId);
        }

        if (loc == null)
        {
            PublishExploreEvent("Explore failed: LocationService is null", GameDebugSeverity.Error);
            _completedThisCycle = false;
            yield break;
        }

        Vector3 targetPos = locations.GetWorldPosition(locationId);
        int locDanger = locations.GetDanger(locationId);

        _lastWorkPos = targetPos;
        _lastWorkLocationId = locationId;

        PublishExploreEvent("Explore");

        _lastWorkLocationDangerTier = Mathf.Clamp(locDanger, 0, 5);

        if (!TrySetDestination(targetPos))
        {
            PublishExploreEvent("Explore failed: no undiscovered location", GameDebugSeverity.Warning);
            _completedThisCycle = false;
            yield break;
        }

        yield return WaitUntilArrivedSafe(arriveTimeoutSec);

        locations.AddVisit(locationId);
        locations.RegisterWorker(locationId, agentId, task.taskId);

        if (task.durationSec > 0f)
            yield return new WaitForSeconds(task.durationSec);

        locations.DiscoverLocation(locationId);
        PublishExploreEvent("Discovered");
        Log($"Explore success: {task.taskId}");

        locations.AddTaskCompleted(locationId);
        locations.RemoveWorker(locationId, agentId, task.taskId);

        _completedThisCycle = true;

    }


    private IEnumerator DoSurveyKnownLocation(TaskInstance task)
    {
        _completedThisCycle = false;

        var locations = GameInstaller.LocationService;
        if (locations == null)
        {
            Log("SurveyKnown failed: LocationService is null");
            _completedThisCycle = false;
            yield break;
        }

        string locationId = task.targetLocationId;
        LocationModel loc = null;

        if (!string.IsNullOrWhiteSpace(locationId))
        {
            loc = locations.GetLocation(locationId);
        }
        else
        {
            locationId = locations.FindRandomDiscoveredLocationWithPotentialResource();

            if (string.IsNullOrWhiteSpace(locationId))
                locationId = locations.FindRandomDiscoveredLocationId();

            if (!string.IsNullOrWhiteSpace(locationId))
                loc = locations.GetLocation(locationId);
        }

        if (loc == null)
        {
            Log("SurveyKnown failed: no discovered location found");
            _completedThisCycle = false;
            yield break;
        }

        Vector3 targetPos = locations.GetWorldPosition(locationId);
        int locDanger = locations.GetDanger(locationId);

        _lastWorkPos = targetPos;
        _lastWorkLocationId = locationId;

        PublishSurveyEvent("Survey");

        _lastWorkLocationDangerTier = Mathf.Clamp(locDanger, 0, 5);

        if (!TrySetDestination(targetPos))
        {
            PublishSurveyEvent("Survey: nothing found");
            _completedThisCycle = false;
            yield break;
        }

        yield return WaitUntilArrivedSafe(arriveTimeoutSec);

        locations.AddVisit(locationId);
        locations.RegisterWorker(locationId, agentId, task.taskId);

        if (task.durationSec > 0f)
            yield return new WaitForSeconds(task.durationSec);

        var survey = GameInstaller.SurveyOutcome;
        if (survey == null)
        {
            locations.RemoveWorker(locationId, agentId, task.taskId);
            Log("SurveyKnown failed: SurveyOutcomeService is null");
            _completedThisCycle = false;
            yield break;
        }

        var outcome = survey.Roll(loc);

        if (outcome.type == SurveyOutcomeType.RevealHiddenResource)
        {
            bool revealed = locations.TryRevealRandomPotentialResource(locationId);
            PublishSurveyEvent("Resource found");
        }
        else if (outcome.type == SurveyOutcomeType.BonusGold)
        {
            if (outcome.goldAmount > 0)
            {
                _cargo.Add("gold", outcome.goldAmount);
                PublishEconomyEvent($"+{outcome.goldAmount} gold");
                PublishSurveyEvent("Bonus gold");
            }
        }
        else if (outcome.type == SurveyOutcomeType.Nothing)
        {
            PublishSurveyEvent("Nothing");
        }
        else if (outcome.type == SurveyOutcomeType.Danger)
        {
            PublishSurveyEvent("Danger", GameDebugSeverity.Warning);
        }

        locations.AddTaskCompleted(locationId);
        locations.RemoveWorker(locationId, agentId, task.taskId);

        _completedThisCycle = true;
    }


    private IEnumerator DoGather(TaskInstance task)
    {
        _completedThisCycle = false;

        if (string.IsNullOrEmpty(task.resourceId))
        {
            PublishTaskEvent("Gather blocked: missing resourceId", GameDebugSeverity.Warning);
            _completedThisCycle = false;
            yield break;
        }

        var locations = GameInstaller.LocationService;
        if (locations == null)
        {
            PublishTaskEvent("Gather blocked: LocationService is null", GameDebugSeverity.Error);
            _completedThisCycle = false;
            yield break;
        }

        string locationId = task.targetLocationId;
        LocationModel loc = null;

        if (!string.IsNullOrWhiteSpace(locationId))
        {
            loc = locations.GetLocation(locationId);
            if (loc == null)
            {
                PublishTaskEvent($"Gather failed: no work spot for {task.resourceId}", GameDebugSeverity.Warning);
                _completedThisCycle = false;
                yield break;
            }
        }
        else
        {
            locationId = locations.FindAnyLocationForResource(task.resourceId, onlyDiscovered: true, onlyUnlocked: true);
            if (string.IsNullOrWhiteSpace(locationId))
            {
                PublishTaskEvent($"Gather failed: no work spot for {task.resourceId}", GameDebugSeverity.Warning);
                _completedThisCycle = false;
                yield break;
            }

            loc = locations.GetLocation(locationId);
            if (loc == null)
            {
                PublishTaskEvent($"Gather failed: no work spot for {task.resourceId}", GameDebugSeverity.Warning);
                _completedThisCycle = false;
                yield break;
            }
        }

        if (!locations.HasUnlockedResource(locationId, task.resourceId))
        {
            PublishTaskEvent($"Gather failed: resource locked {task.resourceId}", GameDebugSeverity.Warning);
            _completedThisCycle = false;
            yield break;
        }

        Vector3 targetPos = locations.GetWorldPosition(locationId);
        int locDanger = locations.GetDanger(locationId);

        Log($"Gather {task.resourceId} at location={locationId} amount={task.baseAmount}");
        _lastWorkPos = targetPos;
        _lastWorkLocationId = locationId;
        PublishTaskEvent("Gather");

        _lastWorkLocationDangerTier = Mathf.Clamp(locDanger, 0, 5);

        if (!TrySetDestination(targetPos))
        {
            Log($"Gather refused: invalid destination location={locationId}");
            PublishTaskEvent("Gather failed: invalid destination", GameDebugSeverity.Warning);
            _completedThisCycle = false;
            yield break;
        }

        yield return WaitUntilArrivedSafe(arriveTimeoutSec);

        locations.AddVisit(locationId);
        locations.RegisterWorker(locationId, agentId, task.taskId);

        if (task.durationSec > 0f)
            yield return new WaitForSeconds(task.durationSec);

        if (task.baseAmount <= 0)
        {
            locations.RemoveWorker(locationId, agentId, task.taskId);
            Log($"Gather produced nothing: baseAmount={task.baseAmount}");
            PublishTaskEvent("Gather produced nothing", GameDebugSeverity.Warning);
            _completedThisCycle = false;
            yield break;
        }

        _cargo.Add(task.resourceId, task.baseAmount);
        locations.AddResourceGathered(locationId, task.resourceId, task.baseAmount);
        locations.AddTaskCompleted(locationId);
        locations.RemoveWorker(locationId, agentId, task.taskId);

        PublishEconomyEvent($"+{task.baseAmount} {task.resourceId}");
        Log($"Gather success: {task.resourceId} x{task.baseAmount}");

        _completedThisCycle = true;
        Log($"Gather done: +{task.baseAmount} {task.resourceId} (cargo now updated)");
    }

    private IEnumerator DoGoHome()
    {
        if (homePoint == null) yield break;

        if (!TrySetDestination(homePoint.position))
        {
            Log($"Return home refused: invalid home destination");
            yield break;
        }

        yield return WaitUntilArrivedSafe(arriveTimeoutSec);
    }

    private void CommitCargoToTreasury()
    {
        if (_treasury == null) return;
        var snap = _cargo.Snapshot();
            if (snap.Count == 0) return;
        
            foreach (var kv in snap)
            {
             if (kv.Value <= 0) continue;
            _treasury.Add(kv.Key, kv.Value);
            Log($"HomeCommit: delivered +{kv.Value} {kv.Key}");
            }
        
        _cargo.Clear();
        
    }

    // -------------------- NAVMESH SAFETY --------------------

    private bool TrySetDestination(Vector3 pos)
    {
        if (agent == null) return false;

        // якщо агент не на навмеші — SetDestination може поводитись дивно
        if (!agent.isOnNavMesh)
        {
            Log($"NavMesh warning: agent is not on NavMesh");
            return false;
        }

        // ✅ 1) Спробувати "приклеїти" ціль до NavMesh (інакше CalculatePath часто дає PathPartial/Invalid)
        Vector3 target = pos;
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            target = hit.position;
        }
        else
        {
            // якщо навіть SamplePosition не знайшов NavMesh поруч — ціль реально невалідна
            return false;
        }

        // ✅ 2) Перевірка, що точка достижима
        var path = new UnityEngine.AI.NavMeshPath();
        if (!agent.CalculatePath(target, path))
            return false;

        if (path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
            return false;

        agent.isStopped = false;
        agent.SetDestination(target);
        return true;
    }

    private IEnumerator WaitUntilArrivedSafe(float timeoutSec)
    {
        float t = 0f;

        // очікуємо, поки порахує шлях
        while (agent.pathPending)
        {
            t += Time.deltaTime;
            if (t >= timeoutSec) { Log($"Arrive timeout (pathPending)"); yield break; }
            yield return null;
        }

        // якщо раптом шлях невалідний
        if (agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            Log($"Arrive aborted: pathStatus={agent.pathStatus}");
            yield break;
        }

        // чекаємо дистанцію
        var dest = agent.destination;

        while (true)
        {
            t += Time.deltaTime;
            if (t >= timeoutSec)
            {
                Log($"Arrive timeout (distance) rem={agent.remainingDistance:0.00} stop={agent.stoppingDistance:0.00} dist={Vector3.Distance(transform.position, dest):0.00}");
                yield break;
            }

            // 1) Якщо remainingDistance норм — використовуємо його
            if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, arriveDistance))
                break;

            // 2) Fallback: фактична дистанція до destination
            if (Vector3.Distance(transform.position, dest) <= arriveDistance)
                break;

            yield return null;
        }


        // дочекатися “зупинки”
        while (agent.velocity.sqrMagnitude > minVelocitySqr)
        {
            t += Time.deltaTime;
            if (t >= timeoutSec)
                break; // не yield break, бо ми вже фактично "прибули"
            yield return null;
        }

    }

    // -------------------- LOGGING --------------------

    private void Log(string msg)
    {
        GameDebug.Info(
            GameDebugChannel.Villager,
            $"[{agentId}] {msg}",
            _lastWorkPos,
            _lastWorkLocationId,
            agentId
        );

        _log?.Push($"[{agentId}] {msg}");
    }


    private void FinalizeTask(TaskInstance task, bool success)
    {
        // --- Escrow + Cargo settlement ---
        if (_treasury != null && task.wageGold > 0)
        {
            if (success)
            {
                // Commit rewards ONLY on success
                CommitCargoToTreasury();

                // Pay wage from locked escrow
                _treasury.ConsumeLockedGold(task.wageGold);
                Log($"Wage paid: +{task.wageGold} gold");
            }
            else
            {
                // Refund escrow back to available
                _treasury.RefundGold(task.wageGold);

                // Fail cannot yield profit
                _cargo.Clear();
                Log($"Task failed -> wage refunded: {task.wageGold} gold");
            }
        }
        else
        {
            // Even if wage=0, fail should not keep cargo
            if (!success)
                _cargo.Clear();
        }

        // --- Board cleanup ALWAYS (exactly once) ---
        _board.Release(task.taskId, agentId);

        // Remove only runtime tasks
        if (!string.IsNullOrEmpty(task.taskId) && task.taskId.StartsWith("rt_"))
            _board.RemoveTaskRuntime(task.taskId);
    }


    private void HandleDeath(TaskInstance task)
    {
        _completedThisCycle = false;

        // escrow: забираємо з locked у record
        if (_treasury != null && task.wageGold > 0)
            _treasury.ConsumeLockedGold(task.wageGold);

        var rec = new CorpseRecord
        {
            agentId = agentId,
            worldPos = _lastWorkPos,
            escrowGold = task.wageGold,
            cargo = _cargo.Snapshot()
        };

        GameInstaller.Corpses?.AddCorpse(rec);

        if (!string.IsNullOrWhiteSpace(task.targetLocationId) && GameInstaller.LocationService != null)
        {
            GameInstaller.LocationService.AddVillagerDead(task.targetLocationId);
            GameInstaller.LocationService.RemoveWorker(task.targetLocationId, agentId, task.taskId);
        }

        _cargo.Clear();

        Log($"PENALTY: DEATH at {_lastWorkLocationId} escrow={task.wageGold}");

        // cleanup board (без FinalizeTask, бо він робить refund/commit)
        _board.Release(task.taskId, agentId);
        if (!string.IsNullOrEmpty(task.taskId) && task.taskId.StartsWith("rt_"))
            _board.RemoveTaskRuntime(task.taskId);

        _roster?.SetStatus(agentId, VillagerStatus.Idle, task.taskId, "DEAD");

        StopBrain(); // зупиняємо корутину цього агента
    }

    private void HandleLost(TaskInstance task)
    {
        _completedThisCycle = false;

        if (_treasury != null && task.wageGold > 0)
            _treasury.ConsumeLockedGold(task.wageGold);

        var hidden = PenaltyRoll.RollLostHidden(task.riskTier);

        var rec = new LostRecord
        {
            agentId = agentId,
            lastSeenPos = _lastWorkPos,
            escrowGold = task.wageGold,
            cargo = _cargo.Snapshot(),
            hiddenOutcome = hidden
        };

        GameInstaller.Lost?.AddLost(rec);

        if (!string.IsNullOrWhiteSpace(task.targetLocationId) && GameInstaller.LocationService != null)
        {
            GameInstaller.LocationService.AddVillagerLost(task.targetLocationId);
            GameInstaller.LocationService.RemoveWorker(task.targetLocationId, agentId, task.taskId);
        }

        _cargo.Clear();

        Log($"PENALTY: LOST at {_lastWorkLocationId} hidden={hidden} escrow={task.wageGold}");

        _board.Release(task.taskId, agentId);
        if (!string.IsNullOrEmpty(task.taskId) && task.taskId.StartsWith("rt_"))
            _board.RemoveTaskRuntime(task.taskId);

        _roster?.SetStatus(agentId, VillagerStatus.Idle, task.taskId, "LOST");

        StopBrain();
    }

    private int GetTaskLocationDanger(TaskInstance task)
    {
        if (task == null) return 0;

        if (!string.IsNullOrWhiteSpace(task.targetLocationId) && GameInstaller.LocationService != null)
            return Mathf.Clamp(GameInstaller.LocationService.GetDanger(task.targetLocationId), 0, 5);

        return Mathf.Clamp(_lastWorkLocationDangerTier, 0, 5);
    }



    private void PublishTaskEvent(string text, GameDebugSeverity severity = GameDebugSeverity.Info)
    {
        var msg = new GameDebugMessage(
            GameDebugChannel.Task,
            severity,
            $"{text}",
            _lastWorkPos,
            _lastWorkLocationId,
            agentId,
            1.6f
        );

        GameDebug.Publish(msg);
    }

    private void PublishExploreEvent(string text, GameDebugSeverity severity = GameDebugSeverity.Info)
    {
        var msg = new GameDebugMessage(
            GameDebugChannel.Explore,
            severity,
            $"{text}",
            _lastWorkPos,
            _lastWorkLocationId,
            agentId,
            1.6f
        );

        GameDebug.Publish(msg);
    }

    private void PublishSurveyEvent(string text, GameDebugSeverity severity = GameDebugSeverity.Info)
    {
        var msg = new GameDebugMessage(
            GameDebugChannel.Survey,
            severity,
            $"{text}",
            _lastWorkPos,
            _lastWorkLocationId,
            agentId,
            1.6f
        );

        GameDebug.Publish(msg);
    }

    private void PublishEconomyEvent(string text, GameDebugSeverity severity = GameDebugSeverity.Info)
    {
        var msg = new GameDebugMessage(
            GameDebugChannel.Economy,
            severity,
            $"{text}",
            _lastWorkPos,
            _lastWorkLocationId,
            agentId,
            1.6f
        );

        GameDebug.Publish(msg);
    }



}
