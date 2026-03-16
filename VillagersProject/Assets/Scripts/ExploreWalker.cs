using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class ExploreWalker : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform homePoint;

    [Header("Timings")]
    [SerializeField] private float workDurationSec = 3f;
    [SerializeField] private float idleBetweenTripsSec = 1f;

    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (homePoint == null)
        {
            Debug.LogError("[ExploreWalker] homePoint is not set!");
            enabled = false;
            return;
        }

        StartCoroutine(RunLoop());
    }

    private IEnumerator RunLoop()
    {
        while (true)
        {
            var locations = GameInstaller.LocationService;
            if (locations == null)
                yield break;

            string locationId = locations.FindRandomUnknownLocationId();
            if (string.IsNullOrWhiteSpace(locationId))
                yield break;

            Vector3 targetPos = locations.GetWorldPosition(locationId);

            if (agent == null || !agent.isOnNavMesh)
                yield break;

            agent.SetDestination(targetPos);
            yield return WaitUntilArrived();

            yield return new WaitForSeconds(workDurationSec);

            var outcome = GameInstaller.ExploreOutcome.Roll();

            if (outcome.type == ExploreOutcomeType.Nothing)
            {
                locations.DiscoverLocation(locationId);
            }
            else if (outcome.type == ExploreOutcomeType.Reward)
            {
                GameInstaller.Treasury.Add(outcome.resourceId, outcome.amount);
                locations.DiscoverLocation(locationId);
            }
            else
            {
                yield return new WaitForSeconds(outcome.returnDelaySec);
            }

            agent.SetDestination(homePoint.position);
            yield return WaitUntilArrived();

            yield return new WaitForSeconds(idleBetweenTripsSec);
        }
    }

    private IEnumerator WaitUntilArrived()
    {
        while (agent.pathPending)
            yield return null;

        while (agent.remainingDistance > agent.stoppingDistance)
            yield return null;

        while (agent.velocity.sqrMagnitude > 0.01f)
            yield return null;
    }
}