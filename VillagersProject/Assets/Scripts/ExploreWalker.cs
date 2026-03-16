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
                yield break;
            }

            Debug.Log($"[ExploreWalker] Going to spot={spot.spotId}");

            // 2) Go to spot
            agent.SetDestination(spot.transform.position);
            yield return WaitUntilArrived();

            yield return new WaitForSeconds(workDurationSec);


            var outcome = GameInstaller.ExploreOutcome.Roll();

            if (outcome.type == ExploreOutcomeType.Nothing)
            {
                
            }
            else if (outcome.type == ExploreOutcomeType.Reward)
            {
                GameInstaller.Treasury.Add(outcome.resourceId, outcome.amount);
            }
            else // Danger
            {
                yield return new WaitForSeconds(outcome.returnDelaySec);
            }



            // 3) Return home
            agent.SetDestination(homePoint.position);
            yield return WaitUntilArrived();

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

