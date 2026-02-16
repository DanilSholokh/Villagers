using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class VillagerAgentBrain : MonoBehaviour
{
    [SerializeField] private string agentId = "A";
    [SerializeField] private Transform homePoint;

    private NavMeshAgent agent;
    private Coroutine loop;
    private bool started;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // ❗ ВАЖЛИВО: OnEnable/Start НЕ запускають логіку

    public void Begin()
    {
        if (started) return;
        started = true;

        if (homePoint == null)
        {
            Debug.LogError($"[VillagerAgentBrain] homePoint is not set! agent={agentId}");
            return;
        }

        if (GameInstaller.TaskBoard == null)
        {
            Debug.LogError($"[VillagerAgentBrain] TaskBoard is null at Begin! agent={agentId}");
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
            var task = TaskSelectionLogic.PickBest(GameInstaller.TaskBoard);
            if (task == null)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            if (!GameInstaller.TaskBoard.TryReserve(task.taskId, agentId))
            {
                yield return null;
                continue;
            }

            Debug.Log($"[Agent {agentId}] Picked task={task.taskId} ({task.displayName})");

            if (task.type == TaskType.Explore)
            {
                var spot = GameInstaller.ExploreRegistry.GetRandomSpotWeighted();
                Debug.Log($"[Agent {agentId}] Explore to spot={spot.spotId}");

                agent.SetDestination(spot.transform.position);
                yield return WaitUntilArrived();

                yield return new WaitForSeconds(task.durationSec);

                var outcome = GameInstaller.ExploreOutcome.Roll();
                if (outcome.type == ExploreOutcomeType.Nothing)
                {
                    Debug.Log($"[Agent {agentId}] Explore outcome: Nothing");
                }
                else if (outcome.type == ExploreOutcomeType.Reward)
                {
                    Debug.Log($"[Agent {agentId}] Explore outcome: Reward +{outcome.amount} {outcome.resourceId}");
                    GameInstaller.Treasury.Add(outcome.resourceId, outcome.amount);
                }
                else
                {
                    Debug.Log($"[Agent {agentId}] Explore outcome: Danger (+{outcome.returnDelaySec:0.0}s)");
                    yield return new WaitForSeconds(outcome.returnDelaySec);
                }
            }
            else // Gather
            {
                var spot = GameInstaller.ExploreRegistry.PickGatherSpotWeighted(task.resourceId);

                if (spot == null)
                {
                    Debug.Log($"[Agent {agentId}] Gather refused: no spot for resource={task.resourceId} task={task.taskId}");
                    GameInstaller.TaskBoard.Release(task.taskId, agentId);
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                Debug.Log($"[Agent {agentId}] Gather -> spot={spot.spotId} res={task.resourceId}");

                agent.SetDestination(spot.transform.position);
                yield return WaitUntilArrived();

                Debug.Log($"[Agent {agentId}] Gather working {task.durationSec:0.0}s");
                yield return new WaitForSeconds(task.durationSec);

                if (!string.IsNullOrEmpty(task.resourceId) && task.baseAmount > 0)
                {
                    Debug.Log($"[Agent {agentId}] Gather result: +{task.baseAmount} {task.resourceId}");
                    GameInstaller.Treasury.Add(task.resourceId, task.baseAmount);
                }
            }


            agent.SetDestination(homePoint.position);
            yield return WaitUntilArrived();

            GameInstaller.TaskBoard.Release(task.taskId, agentId);

            yield return new WaitForSeconds(0.25f);
        }
    }

    private IEnumerator WaitUntilArrived()
    {
        while (agent.pathPending) yield return null;
        while (agent.remainingDistance > agent.stoppingDistance) yield return null;
        while (agent.velocity.sqrMagnitude > 0.01f) yield return null;
    }
}
