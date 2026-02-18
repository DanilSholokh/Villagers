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

    private TaskBoardService _board;
    private ExploreSpotRegistry _spots;
    private TreasuryService _treasury;
    private EventLogService _log;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // ❗ ВАЖЛИВО: OnEnable/Start НЕ запускають логіку

    public void Begin(TaskBoardService board, ExploreSpotRegistry spots, TreasuryService treasury, EventLogService log)
    {
        _board = board;
        _spots = spots;
        _treasury = treasury;
        _log = log;

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

            // 3) Execute
            if (task.type == TaskType.Explore)
                yield return DoExplore(task);
            else
                yield return DoGather(task);

            // 4) Return home
            yield return DoGoHome();

            // 5) Release (після повернення)
            _board.Release(task.taskId, agentId);

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
        }
        else if (outcome.type == ExploreOutcomeType.Reward)
        {
            Log($"Explore outcome: Reward +{outcome.amount} {outcome.resourceId}");
            if (_treasury != null)
                _treasury.Add(outcome.resourceId, outcome.amount);
        }
        else
        {
            Log($"Explore outcome: Danger (+{outcome.returnDelaySec:0.0}s)");
            yield return new WaitForSeconds(outcome.returnDelaySec);
        }
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
        }
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

        // перевірка, що точка достижима
        var path = new NavMeshPath();
        if (!agent.CalculatePath(pos, path))
            return false;

        if (path.status != NavMeshPathStatus.PathComplete)
            return false;

        agent.isStopped = false;
        agent.SetDestination(pos);
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
