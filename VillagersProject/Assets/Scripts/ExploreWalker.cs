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
            // 1) Pick random spot
            var spot = GameInstaller.ExploreRegistry.GetRandomSpotWeighted();
            if (spot == null)
            {
                Debug.LogError("[ExploreWalker] No explore spots in registry.");
                yield break;
            }

            Debug.Log($"[ExploreWalker] Going to spot={spot.spotId}");

            // 2) Go to spot
            agent.SetDestination(spot.transform.position);
            yield return WaitUntilArrived();

            Debug.Log($"[ExploreWalker] Arrived spot={spot.spotId}, working {workDurationSec:0.0}s");
            yield return new WaitForSeconds(workDurationSec);


            var outcome = GameInstaller.ExploreOutcome.Roll();

            if (outcome.type == ExploreOutcomeType.Nothing)
            {
                Debug.Log("[ExploreWalker] Outcome: Nothing");
            }
            else if (outcome.type == ExploreOutcomeType.Reward)
            {
                Debug.Log($"[ExploreWalker] Outcome: Reward +{outcome.amount} {outcome.resourceId}");
                GameInstaller.Treasury.Add(outcome.resourceId, outcome.amount);
            }
            else // Danger
            {
                Debug.Log($"[ExploreWalker] Outcome: Danger, return delayed +{outcome.returnDelaySec:0.0}s");
                yield return new WaitForSeconds(outcome.returnDelaySec);
            }



            // 3) Return home
            Debug.Log("[ExploreWalker] Returning home");
            agent.SetDestination(homePoint.position);
            yield return WaitUntilArrived();

            Debug.Log($"[ExploreWalker] Home. Next trip in {idleBetweenTripsSec:0.0}s");
            yield return new WaitForSeconds(idleBetweenTripsSec);
        }
    }

    private IEnumerator WaitUntilArrived()
    {
        // чекаЇмо поки агент реально порахуЇ шл€х
        while (agent.pathPending)
            yield return null;

        // чекаЇмо поки д≥йде
        while (agent.remainingDistance > agent.stoppingDistance)
            yield return null;

        // ≥нколи агент Уще рухаЇтьс€Ф нав≥ть коли дистанц≥€ маленька
        while (agent.velocity.sqrMagnitude > 0.01f)
            yield return null;
    }
}

