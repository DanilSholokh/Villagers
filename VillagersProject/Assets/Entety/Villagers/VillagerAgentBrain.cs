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

    [SerializeField] private float arriveDistance = 0.6f;


    private NavMeshAgent agent;
    private Coroutine loop;

    private bool started;
    private bool _completedThisCycle;


    private TaskBoardService _board;
    private ExploreSpotRegistry _spots;
    private TreasuryService _treasury;
    private EventLogService _log;


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

            Log($"Picked task={task.taskId} ({task.displayName})");
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




            // 4) Return home
            _roster?.SetStatus(agentId, VillagerStatus.ReturningHome, task.taskId, task.displayName);
            yield return DoGoHome();

            

            // 5) Release (після повернення)
            _board.Release(task.taskId, agentId);

            // VARIANT 1: runtime таски одноразові — після успіху видаляємо з борди
            if (_completedThisCycle && task.taskId != null && task.taskId.StartsWith("rt_"))
            {
                _board.RemoveTaskRuntime(task.taskId);
            }

            _roster?.SetStatus(agentId, VillagerStatus.Idle);

            yield return new WaitForSeconds(afterCycleDelay);

        }

    }

    // -------------------- ACTIONS --------------------

    private IEnumerator DoExplore(TaskInstance task)
    {
        var spot = _spots.GetRandomSpotWeighted();
        if (spot == null)
        {
            Log($"Explore failed: no spot (registry empty?)");
            // release одразу, щоб слот не висів
            _board.Release(task.taskId, agentId);
            yield return new WaitForSeconds(afterCycleDelay);
            yield break;
        }

        Log($"Explore to spot={spot.spotId}");

        if (!TrySetDestination(spot.transform.position))
        {
            Log($"Explore refused: invalid destination spot={spot.spotId}");
            _board.Release(task.taskId, agentId);
            yield return new WaitForSeconds(afterCycleDelay);
            yield break;
        }

        yield return WaitUntilArrivedSafe(arriveTimeoutSec);

        yield return new WaitForSeconds(task.durationSec);

        if (GameInstaller.ExploreOutcome == null)
        {
            Log("ExploreOutcome is null (installer not initialized?)");
            yield break;
        }

        var outcome = GameInstaller.ExploreOutcome.Roll();

        if (outcome.type == ExploreOutcomeType.Nothing)
        {
            Log($"Explore outcome: Nothing");
            GameInstaller.Progression?.AddAchievement(agentId, 1);
        }
        else if (outcome.type == ExploreOutcomeType.Reward)
        {
            Log($"Explore outcome: Reward +{outcome.amount} {outcome.resourceId}");
            if (_treasury != null)
                _treasury.Add(outcome.resourceId, outcome.amount);
            GameInstaller.Progression?.AddAchievement(agentId, 3);
        }
        else
        {
            Log($"Explore outcome: Danger (+{outcome.returnDelaySec:0.0}s)");
            yield return new WaitForSeconds(outcome.returnDelaySec);
        }

        _completedThisCycle = true;

    }

    private IEnumerator DoGather(TaskInstance task)
    {
        var spot = _spots.PickGatherSpotWeighted(task.resourceId);

        if (spot == null)
        {
            Log($"Gather refused: no spot for resource={task.resourceId} task={task.taskId}");
            _board.Release(task.taskId, agentId);
            yield return new WaitForSeconds(gatherNoSpotDelay);
            yield break;
        }

        Log($"Gather -> spot={spot.spotId} res={task.resourceId}");

        if (!TrySetDestination(spot.transform.position))
        {
            Log($"Gather refused: invalid destination spot={spot.spotId}");
            _board.Release(task.taskId, agentId);
            yield return new WaitForSeconds(gatherNoSpotDelay);
            yield break;
        }

        yield return WaitUntilArrivedSafe(arriveTimeoutSec);

        Log($"Gather working {task.durationSec:0.0}s");
        yield return new WaitForSeconds(task.durationSec);

        if (!string.IsNullOrEmpty(task.resourceId) && task.baseAmount > 0)
        {
            Log($"Gather result: +{task.baseAmount} {task.resourceId}");
            if (_treasury != null)
                _treasury.Add(task.resourceId, task.baseAmount);

            GameInstaller.Progression?.AddAchievement(agentId, 2);
        }

        _completedThisCycle = true;

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
        Debug.Log($"[Agent {agentId}] {msg}");
        _log?.Push($"[Agent {agentId}] {msg}");
    }
}
