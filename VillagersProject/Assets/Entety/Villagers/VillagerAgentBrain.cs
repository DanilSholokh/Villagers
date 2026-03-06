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

    private int _lastWorkSpotDangerTier;

    private NavMeshAgent agent;
    private Coroutine loop;

    private bool started;
    private bool _completedThisCycle;


    private TaskBoardService _board;
    private ExploreSpotRegistry _spots;
    private TreasuryService _treasury;
    private EventLogService _log;

    private Vector3 _lastWorkPos;
    private string _lastWorkSpotId;

    private VillagerRosterService _roster;

    public string AgentId => agentId;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // ❗ ВАЖЛИВО: OnEnable/Start НЕ запускають логіку

    public void Begin(TaskBoardService board, ExploreSpotRegistry spots, TreasuryService treasury, EventLogService log, VillagerRosterService roster)
    {
        _board = board;
        _spots = spots;
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

        if (_spots == null)
        {
            Debug.LogError($"[VillagerAgentBrain] ExploreSpotRegistry is null at Begin! agent={agentId}");
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

            if (task.type == TaskType.Explore)
            {
                _roster?.SetStatus(agentId, VillagerStatus.Working, task.taskId, task.displayName);
                yield return DoExplore(task);
            }
            else
            {
                _roster?.SetStatus(agentId, VillagerStatus.Working, task.taskId, task.displayName);
                yield return DoGather(task);
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
                    HandleDeath(task);
                    yield break;
                }

                if (penalty == PenaltyType.Lost)
                {
                    HandleLost(task);
                    yield break;
                }

                _completedThisCycle = false;

                _roster?.SetStatus(agentId, VillagerStatus.ReturningHome, task.taskId, "FAILED");
                yield return DoGoHome();

                _cargo.Clear();

                Log($"Task FAILED. Penalty=None riskTier={task.riskTier} locDanger={locDanger}");
                FinalizeTask(task, false);

                _roster?.SetStatus(agentId, VillagerStatus.Idle);
                yield return new WaitForSeconds(afterCycleDelay);
                continue;
            }

            // 4) Success path: return home + finalize
            _roster?.SetStatus(agentId, VillagerStatus.ReturningHome, task.taskId, task.displayName);
            yield return DoGoHome();

            FinalizeTask(task, _completedThisCycle);

            _roster?.SetStatus(agentId, VillagerStatus.Idle);
            yield return new WaitForSeconds(afterCycleDelay);
        }
    }

    // -------------------- ACTIONS --------------------

    private IEnumerator DoExplore(TaskInstance task)
    {
        _completedThisCycle = true;

        // 1) Registry
        var registry = GameInstaller.ExploreRegistry;
        if (registry == null)
        {
            Log("Explore failed: ExploreRegistry is null");
            _completedThisCycle = false;
            yield break;
        }

        // 2) Pick spot
        ExploreSpotAuthoring spot = null;

        if (!string.IsNullOrWhiteSpace(task.targetSpotId))
        {
            spot = registry.GetSpotById(task.targetSpotId);
            if (spot == null)
            {
                Log($"Explore failed: targetSpotId not found={task.targetSpotId}");
                _completedThisCycle = false;
                yield break;
            }
        }
        else
        {
            // Якщо ти ще не ввів knowledge — тут може бути просто registry.GetRandomSpotWeighted()
            spot = registry.GetRandomUndiscoveredWeighted(GameInstaller.Knowledge);
        }


        Log($"Explore to spot={spot.spotId} ({spot.displayName}) danger={spot.dangerTier}");
        _lastWorkPos = spot.transform.position;
        _lastWorkSpotId = spot.spotId;
        _lastWorkSpotDangerTier = Mathf.Clamp(spot.dangerTier, 0, 5);

        // 3) Move to spot
        if (!TrySetDestination(spot.transform.position))
        {
            Log($"Explore refused: invalid destination spot={spot.spotId}");
            _completedThisCycle = false;
            yield break;
        }

        // Дочекайся прибуття (якщо у тебе є такий метод/цикл — підстав свій)
        yield return WaitUntilArrivedSafe(arriveTimeoutSec);
        GameInstaller.Knowledge?.Discover(spot.spotId);

        // 4) Do work (як зараз у тебе — таймером)
        if (task.durationSec > 0f)
            yield return new WaitForSeconds(task.durationSec);

        // 5) Roll outcome
        var outcomeSvc = GameInstaller.ExploreOutcome;
        if (outcomeSvc == null)
        {
            Log("Explore failed: ExploreOutcome is null");
            _completedThisCycle = false;
            yield break;
        }

        var outcome = outcomeSvc.Roll();

        if (outcome.type == ExploreOutcomeType.Nothing)
        {
            Log("Explore outcome: Nothing");

            // IMPORTANT: за поточним рішенням ми не міняємо дизайн:
            // Nothing вважається "completed", тобто _completedThisCycle лишається true.
            // Якщо потім вирішиш, що Nothing=fail — це міняється тут.
            yield break;
        }

        if (outcome.type == ExploreOutcomeType.Danger)
        {
            Log("Explore outcome: Danger");

            // Поки без penalties — просто completed (як у тебе зараз).
            yield break;
        }

        if (outcome.type == ExploreOutcomeType.Reward)
        {
            Log($"Explore outcome: Reward +{outcome.amount} {outcome.resourceId}");

            // ✅ Це і є твій фікс P0: додаємо саме outcome, а не task.resourceId/baseAmount
            _cargo.Add(outcome.resourceId, outcome.amount);

            GameInstaller.Progression?.AddAchievement(agentId, 3);
            yield break;
        }

        // safety
        Log("Explore outcome: unknown type");
        _completedThisCycle = false;
    }

    private IEnumerator DoGather(TaskInstance task)
    {
        _completedThisCycle = true;

        // 1) Validate resource
        if (string.IsNullOrEmpty(task.resourceId))
        {
            Log($"Gather failed: empty resourceId for task={task.taskId}");
            _completedThisCycle = false;
            yield break;
        }

        // 2) Pick gather spot by resource (як у тебе вже є в ExploreSpotRegistry)
        var registry = GameInstaller.ExploreRegistry;
        ExploreSpotAuthoring spot = null;

        if (!string.IsNullOrWhiteSpace(task.targetSpotId))
        {
            spot = registry.GetSpotById(task.targetSpotId);

            if (spot == null)
            {
                Log($"Gather failed: targetSpotId not found={task.targetSpotId}");
                _completedThisCycle = false;
                yield break;
            }

            // optional safety: ресурс локації співпадає?
            if (!string.IsNullOrWhiteSpace(spot.gatherResourceId) &&
                !string.Equals(spot.gatherResourceId, task.resourceId, System.StringComparison.OrdinalIgnoreCase))
            {
                Log($"Gather failed: spot resource mismatch. spot={spot.spotId} spotRes={spot.gatherResourceId} taskRes={task.resourceId}");
                _completedThisCycle = false;
                yield break;
            }
        }
        else
        {
            spot = registry.PickGatherSpotWeighted(task.resourceId);
        }

        if (spot == null)
        {
            Log($"Gather failed: no gather spot for resource={task.resourceId}");
            _completedThisCycle = false;
            yield break;
        }

        Log($"Gather {task.resourceId} at spot={spot.spotId} amount={task.baseAmount}");
        _lastWorkPos = spot.transform.position;
        _lastWorkSpotId = spot.spotId;

        _lastWorkSpotDangerTier = Mathf.Clamp(spot.dangerTier, 0, 5);

        // 3) Move to spot
        if (!TrySetDestination(spot.transform.position))
        {
            Log($"Gather refused: invalid destination spot={spot.spotId}");
            _completedThisCycle = false;
            yield break;
        }

        yield return WaitUntilArrivedSafe(arriveTimeoutSec);

        // 4) Do work (як зараз — таймером)
        if (task.durationSec > 0f)
            yield return new WaitForSeconds(task.durationSec);

        // 5) Add cargo (як було)
        if (task.baseAmount <= 0)
        {
            Log($"Gather produced nothing: baseAmount={task.baseAmount}");
            // зазвичай це має бути fail, але якщо не хочеш чіпати дизайн — можеш лишити success
            // я ставлю fail, бо інакше це дивно для gather:
            _completedThisCycle = false;
            yield break;
        }

        _cargo.Add(task.resourceId, task.baseAmount);
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
        //Debug.Log($"[Agent {agentId}] {msg}");
        //_log?.Push($"[Agent {agentId}] {msg}");
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

        _cargo.Clear();

        Log($"PENALTY: DEATH at {_lastWorkSpotId} escrow={task.wageGold}");

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

        _cargo.Clear();

        Log($"PENALTY: LOST at {_lastWorkSpotId} hidden={hidden} escrow={task.wageGold}");

        _board.Release(task.taskId, agentId);
        if (!string.IsNullOrEmpty(task.taskId) && task.taskId.StartsWith("rt_"))
            _board.RemoveTaskRuntime(task.taskId);

        _roster?.SetStatus(agentId, VillagerStatus.Idle, task.taskId, "LOST");

        StopBrain();
    }

    private int GetTaskLocationDanger(TaskInstance task)
    {
        if (task == null) return 0;
        if (!string.IsNullOrWhiteSpace(task.targetSpotId))
            return Mathf.Clamp(GameInstaller.ExploreRegistry.GetDangerTier(task.targetSpotId), 0, 5);

        // fallback: якщо таска без targetSpotId, то беремо останню робочу
        return Mathf.Clamp(_lastWorkSpotDangerTier, 0, 5);
    }

}
