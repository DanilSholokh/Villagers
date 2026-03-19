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
    private readonly VillagerTaskSettlementService _taskSettlement = new();

    private readonly TaskEscrowService _taskEscrow = new();
    private TaskEscrowReservation _activeEscrow;

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
            var task = TaskSelectionLogic.PickBest(_board, _treasury);
            if (task == null)
            {
                yield return new WaitForSeconds(noTaskDelay);
                continue;
            }

            // 2) Reserve
            if (!_board.TryReserve(task.taskId, agentId))
            {
                yield return AbortTaskStart(task, null, 0f);
                continue;
            }

            if (!TryReserveTaskStartCost(task))
            {
                yield return AbortTaskStart(task, $"Task start aborted: failed to reserve upfront cost for task={task.taskId}");
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

                Log($"Villager failed task {task.taskId}");
                PublishTaskEvent("Failed", GameDebugSeverity.Warning);

                Log($"Task failed roll: {task.taskId} (risk={task.riskTier}, danger={locDanger})");

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

        if (!TryResolveExploreLocation(task, locations, out string locationId, out LocationModel loc, out string error))
        {
            PublishExploreEvent($"Explore failed: {error}", GameDebugSeverity.Warning);
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

        if (!TryExecuteExploreUnlock(task))
        {
            _completedThisCycle = false;
            yield break;
        }

        locations.DiscoverLocation(locationId);
        PublishExploreEvent("Discovered");
        Log($"Explore success: {task.taskId}");

        locations.AddTaskCompleted(locationId);

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

        if (!TryResolveSurveyLocation(task, locations, out string locationId, out LocationModel loc, out string error))
        {
            Log($"SurveyKnown failed: {error}");
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
            Log("SurveyKnown failed: SurveyOutcomeService is null");
            _completedThisCycle = false;
            yield break;
        }

        var outcome = survey.Roll(loc);

        if (outcome.type == SurveyOutcomeType.RevealHiddenResource)
        {
            bool revealed = locations.TryRevealRandomPotentialResourceDetailed(locationId, out string revealedResourceId);

            if (revealed)
            {
                Log($"SurveyKnown revealed resource '{revealedResourceId}' at location={locationId}");
                PublishSurveyEvent($"Resource found: {revealedResourceId}");
            }
            else
            {
                Log($"SurveyKnown reveal roll failed: no hidden resource revealed at location={locationId}");
                PublishSurveyEvent("Nothing");
            }
        }
        else if (outcome.type == SurveyOutcomeType.BonusGold)
        {
            if (outcome.goldAmount > 0)
            {
                _cargo.Add("gold", outcome.goldAmount);
                Log($"SurveyKnown found bonus gold +{outcome.goldAmount} at location={locationId}");
                PublishEconomyEvent($"+{outcome.goldAmount} gold");
                PublishSurveyEvent($"Bonus gold: +{outcome.goldAmount}");
            }
            else
            {
                PublishSurveyEvent("Nothing");
            }
        }
        else if (outcome.type == SurveyOutcomeType.Nothing)
        {
            Log($"SurveyKnown found nothing at location={locationId}");
            PublishSurveyEvent("Nothing");
        }
        else if (outcome.type == SurveyOutcomeType.Danger)
        {
            Log($"SurveyKnown danger event at location={locationId}");
            PublishSurveyEvent("Danger", GameDebugSeverity.Warning);
        }

        locations.AddTaskCompleted(locationId);

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

        if (!TryResolveGatherLocation(task, locations, out string locationId, out LocationModel loc, out string error))
        {
            PublishTaskEvent($"Gather failed: {error}", GameDebugSeverity.Warning);
            _completedThisCycle = false;
            yield break;
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

        int gatheredAmount = GetGatherAmount(task);

        if (gatheredAmount > 0)
        {
            locations.AddResourceGathered(locationId, task.resourceId, gatheredAmount);
            Log($"Gather complete: {task.resourceId} x{gatheredAmount} at location={locationId}");
            PublishTaskEvent($"Gathered: {task.resourceId} x{gatheredAmount}");
        }
        else
        {
            Log($"Gather complete: no output amount resolved for task={task.taskId}");
            PublishTaskEvent("Gather complete");
        }

        if (gatheredAmount <= 0)
        {
            Log($"Gather produced nothing: gatheredAmount={gatheredAmount}");
            PublishTaskEvent("Gather produced nothing", GameDebugSeverity.Warning);
            _completedThisCycle = false;
            yield break;
        }

        var workBundle = task.GetResolvedWorkOutputBundle();

        if (workBundle == null || workBundle.IsEmpty)
        {
            _cargo.Add(task.resourceId, gatheredAmount);
        }

        locations.AddTaskCompleted(locationId);

        PublishEconomyEvent($"+{gatheredAmount} {task.resourceId}");
        Log($"Gather success: {task.resourceId} x{gatheredAmount}");

        _completedThisCycle = true;
        Log($"Gather done: +{gatheredAmount} {task.resourceId} (cargo now updated)");
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

    private bool TryExecuteExploreUnlock(TaskInstance task)
    {
        var explore = GameInstaller.ExplorationUnlock;
        if (explore == null)
        {
            PublishExploreEvent("Explore failed: ExplorationUnlockService is null", GameDebugSeverity.Error);
            return false;
        }

        if (_treasury == null)
        {
            PublishExploreEvent("Explore failed: Treasury is null", GameDebugSeverity.Error);
            return false;
        }

        if (!explore.TryExecuteUnlock(
            _treasury,
            agentId,
            task != null ? task.taskId : "explore_unlock",
            out var result))
        {
            string message = result != null && !string.IsNullOrWhiteSpace(result.message)
                ? result.message
                : "exploration unlock economy transaction failed";

            PublishExploreEvent($"Explore failed: {message}", GameDebugSeverity.Warning);
            PublishEconomyEvent($"Explore unlock failed: {message}", GameDebugSeverity.Warning);
            return false;
        }

        PublishEconomyEvent(
            $"Explore unlock spent: {EconomyUiTextFormatter.FormatBundle(result.consumed)}",
            GameDebugSeverity.Info
        );

        return true;
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

    private bool TryReserveTaskStartCost(TaskInstance task)
    {
        _activeEscrow = null;

        if (task == null)
        {
            Log("Reserve failed: task is null");
            return false;
        }

        if (_taskEscrow == null)
        {
            Log("Reserve failed: TaskEscrowService is null");
            return false;
        }

        if (_treasury == null)
        {
            Log($"Reserve failed: Treasury is null for task={task.taskId}");
            return false;
        }

        if (!_taskEscrow.TryReserve(task, agentId, _treasury, out var reservation, out var errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
                Log($"Reserve failed: {errorMessage}");
            else
                Log($"Reserve failed: unknown error for task={task.taskId}");

            return false;
        }

        _activeEscrow = reservation;
        return true;
    }

    private void CancelActiveTaskStart(TaskInstance task)
    {
        if (_activeEscrow != null)
        {
            _taskEscrow.SettleFailure(_activeEscrow, _treasury);
            _activeEscrow = null;
        }

        ReleaseTaskWorkerBinding(task);
        CleanupTaskReservation(task);
        _roster?.SetStatus(agentId, VillagerStatus.Idle);
    }

    private void CleanupTaskReservation(TaskInstance task)
    {
        if (task == null || _board == null)
            return;

        _board.Release(task.taskId, agentId);

        if (!string.IsNullOrEmpty(task.taskId) && task.taskId.StartsWith("rt_"))
            _board.RemoveTaskRuntime(task.taskId);
    }



    private IEnumerator AbortTaskStart(TaskInstance task, string reason, float delaySec = -1f)
    {
        if (!string.IsNullOrWhiteSpace(reason))
            Log(reason);

        CancelActiveTaskStart(task);

        if (delaySec < 0f)
            delaySec = noTaskDelay;

        if (delaySec > 0f)
            yield return new WaitForSeconds(delaySec);
        else
            yield return null;
    }


    private void FinalizeTask(TaskInstance task, bool success)
    {
        if (task == null)
        {
            _activeEscrow = null;
            _roster?.SetStatus(agentId, VillagerStatus.Idle);
            return;
        }

        if (success)
        {
            _taskSettlement.ApplySuccess(task, _cargo, _treasury);

            Log($"Task success: {task.taskId}");
            PublishTaskEvent($"Completed: {task.taskId}");

            ApplyTaskSettlement(task, settleEscrowAsSuccess: true, finalTaskLabel: task.displayName);
            return;
        }

        _taskSettlement.ApplyFailure(task, _cargo, _treasury);

        Log($"Task failed: {task.taskId}");
        PublishTaskEvent($"Failed: {task.taskId}", GameDebugSeverity.Warning);

        ApplyTaskSettlement(task, settleEscrowAsSuccess: false, finalTaskLabel: "FAILED");
    }


    private void HandleDeath(TaskInstance task)
    {
        _completedThisCycle = false;

        int escrowGold = _activeEscrow != null ? _activeEscrow.lockedGold : 0;

        var rec = new CorpseRecord
        {
            agentId = agentId,
            worldPos = _lastWorkPos,
            escrowGold = escrowGold,
            cargo = _cargo.Snapshot()
        };

        GameInstaller.Corpses?.AddCorpse(rec);

        if (task != null &&
            !string.IsNullOrWhiteSpace(task.GetResolvedTargetLocationId()) &&
            GameInstaller.LocationService != null)
        {
            GameInstaller.LocationService.AddVillagerDead(task.GetResolvedTargetLocationId());
        }

        _taskSettlement.ApplyDeath(task, _cargo, _treasury);
        _taskEscrow.SettleDeath(_activeEscrow, _treasury);

        Log($"PENALTY: DEATH at {_lastWorkLocationId} escrow={escrowGold}");

        if (task != null)
        {
            ReleaseTaskWorkerBinding(task);
            CleanupTaskReservation(task);
            _roster?.SetStatus(agentId, VillagerStatus.Idle, task.taskId, "DEAD");
        }
        else
        {
            _roster?.SetStatus(agentId, VillagerStatus.Idle, null, "DEAD");
        }

        _activeEscrow = null;
        StopBrain();
    }

    private void HandleLost(TaskInstance task)
    {
        _completedThisCycle = false;

        int riskTier = task != null ? task.riskTier : 0;
        int escrowGold = _activeEscrow != null ? _activeEscrow.lockedGold : 0;
        var hidden = PenaltyRoll.RollLostHidden(riskTier);

        var rec = new LostRecord
        {
            agentId = agentId,
            lastSeenPos = _lastWorkPos,
            escrowGold = escrowGold,
            cargo = _cargo.Snapshot(),
            hiddenOutcome = hidden
        };

        GameInstaller.Lost?.AddLost(rec);

        if (task != null &&
            !string.IsNullOrWhiteSpace(task.GetResolvedTargetLocationId()) &&
            GameInstaller.LocationService != null)
        {
            GameInstaller.LocationService.AddVillagerLost(task.GetResolvedTargetLocationId());
        }

        _taskSettlement.ApplyLost(task, _cargo, _treasury);
        _taskEscrow.SettleLost(_activeEscrow, _treasury);

        Log($"PENALTY: LOST at {_lastWorkLocationId} hidden={hidden} escrow={escrowGold}");

        if (task != null)
        {
            ReleaseTaskWorkerBinding(task);
            CleanupTaskReservation(task);
            _roster?.SetStatus(agentId, VillagerStatus.Idle, task.taskId, "LOST");
        }
        else
        {
            _roster?.SetStatus(agentId, VillagerStatus.Idle, null, "LOST");
        }

        _activeEscrow = null;
        StopBrain();
    }

    private void ApplyTaskSettlement(TaskInstance task, bool settleEscrowAsSuccess, string finalTaskLabel)
    {
        if (task == null)
            return;

        if (settleEscrowAsSuccess)
            _taskEscrow.SettleSuccess(_activeEscrow, _treasury);
        else
            _taskEscrow.SettleFailure(_activeEscrow, _treasury);

        _activeEscrow = null;

        ReleaseTaskWorkerBinding(task);
        CleanupTaskReservation(task);

        _roster?.SetStatus(
            agentId,
            VillagerStatus.Idle,
            task.taskId,
            string.IsNullOrWhiteSpace(finalTaskLabel) ? task.displayName : finalTaskLabel
        );
    }

    private bool TryGetStrictTaskLocation(
    TaskInstance task,
    out string locationId,
    out LocationModel location,
    out LocationService locations,
    out string error)
    {
        locationId = string.Empty;
        location = null;
        locations = GameInstaller.LocationService;
        error = string.Empty;

        if (task == null)
        {
            error = "Task is null";
            return false;
        }

        if (locations == null)
        {
            error = "LocationService is null";
            return false;
        }

        locationId = task.GetResolvedTargetLocationId();
        if (string.IsNullOrWhiteSpace(locationId))
        {
            error = $"Task '{task.taskId}' has empty targetLocationId";
            return false;
        }

        location = locations.GetLocation(locationId);
        if (location == null)
        {
            error = $"Task '{task.taskId}' references missing location '{locationId}'";
            return false;
        }

        return true;
    }


    private bool TryResolveGatherLocation(
    TaskInstance task,
    LocationService locations,
    out string locationId,
    out LocationModel loc,
    out string error)
    {
        locationId = string.Empty;
        loc = null;
        error = string.Empty;

        if (task == null)
        {
            error = "Task is null";
            return false;
        }

        if (locations == null)
        {
            error = "LocationService is null";
            return false;
        }

        if (string.IsNullOrWhiteSpace(task.resourceId))
        {
            error = "missing resourceId";
            return false;
        }

        locationId = task.GetResolvedTargetLocationId();

        if (!string.IsNullOrWhiteSpace(locationId))
            loc = locations.GetLocation(locationId);

        if (loc == null)
        {
            locationId = locations.FindRandomLocationForResource(task.resourceId, true, true);

            if (!string.IsNullOrWhiteSpace(locationId))
                loc = locations.GetLocation(locationId);
        }

        if (loc == null)
        {
            error = $"no unlocked discovered location for resource '{task.resourceId}'";
            return false;
        }

        return true;
    }

    private bool TryResolveSurveyLocation(
        TaskInstance task,
        LocationService locations,
        out string locationId,
        out LocationModel loc,
        out string error)
    {
        locationId = string.Empty;
        loc = null;
        error = string.Empty;

        if (task == null)
        {
            error = "Task is null";
            return false;
        }

        if (locations == null)
        {
            error = "LocationService is null";
            return false;
        }

        locationId = task.GetResolvedTargetLocationId();

        if (!string.IsNullOrWhiteSpace(locationId))
            loc = locations.GetLocation(locationId);

        if (loc == null)
        {
            locationId = locations.FindRandomDiscoveredLocationWithPotentialResource();

            if (string.IsNullOrWhiteSpace(locationId))
                locationId = locations.FindRandomDiscoveredLocationId();

            if (!string.IsNullOrWhiteSpace(locationId))
                loc = locations.GetLocation(locationId);
        }

        if (loc == null)
        {
            error = "no discovered location found";
            return false;
        }

        return true;
    }

    private bool TryResolveExploreLocation(
        TaskInstance task,
        LocationService locations,
        out string locationId,
        out LocationModel loc,
        out string error)
    {
        locationId = string.Empty;
        loc = null;
        error = string.Empty;

        if (task == null)
        {
            error = "Task is null";
            return false;
        }

        if (locations == null)
        {
            error = "LocationService is null";
            return false;
        }

        locationId = task.GetResolvedTargetLocationId();

        if (!string.IsNullOrWhiteSpace(locationId))
            loc = locations.GetLocation(locationId);

        if (loc == null)
        {
            locationId = locations.FindRandomUnknownLocationId();

            if (!string.IsNullOrWhiteSpace(locationId))
                loc = locations.GetLocation(locationId);
        }

        if (loc == null)
        {
            error = "no unknown location found";
            return false;
        }

        return true;
    }


    private void ReleaseTaskWorkerBinding(TaskInstance task)
    {
        if (task == null || GameInstaller.LocationService == null)
            return;

        string locationId = task.GetResolvedTargetLocationId();

        if (!string.IsNullOrWhiteSpace(locationId))
            GameInstaller.LocationService.RemoveWorker(locationId, agentId, task.taskId);
    }

    private void ApplyTaskSettlement(TaskInstance task, bool success)
    {
        if (task == null)
            return;

        if (success)
            _taskEscrow.SettleSuccess(_activeEscrow, _treasury);
        else
            _taskEscrow.SettleFailure(_activeEscrow, _treasury);

        _activeEscrow = null;

        ReleaseTaskWorkerBinding(task);
        CleanupTaskReservation(task);

        _roster?.SetStatus(agentId, VillagerStatus.Idle);
    }


    private int GetGatherAmount(TaskInstance task)
    {
        if (task == null)
            return 0;

        var workBundle = task.GetResolvedWorkOutputBundle();
        if (workBundle != null && !workBundle.IsEmpty && !string.IsNullOrWhiteSpace(task.resourceId))
            return workBundle.GetExactAmount(task.resourceId);

        return Mathf.Max(0, task.baseAmount);
    }

    private int GetTaskLocationDanger(TaskInstance task)
    {
        if (task == null)
            return 0;

        string locationId = task.GetResolvedTargetLocationId();

        if (!string.IsNullOrWhiteSpace(locationId) && GameInstaller.LocationService != null)
            return Mathf.Clamp(GameInstaller.LocationService.GetDanger(locationId), 0, 5);

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
