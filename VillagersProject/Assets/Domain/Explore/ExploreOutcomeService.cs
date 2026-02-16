using UnityEngine;

public enum ExploreOutcomeType
{
    Nothing = 0,
    Reward = 1,
    Danger = 2
}

public struct ExploreOutcome
{
    public ExploreOutcomeType type;

    // Reward
    public string resourceId;
    public int amount;

    // Danger
    public float returnDelaySec;
}

public class ExploreOutcomeService
{
    // Outcome weights (можеш крутити під UX)
    private const float W_NOTHING = 0.45f;
    private const float W_REWARD = 0.40f;
    private const float W_DANGER = 0.15f;

    // Reward weights
    private const float W_WOOD = 0.45f;
    private const float W_STONE = 0.35f;
    private const float W_FISH = 0.20f;

    public ExploreOutcome Roll()
    {
        var o = new ExploreOutcome();

        float total = W_NOTHING + W_REWARD + W_DANGER;
        float r = Random.Range(0f, total);

        if (r < W_NOTHING)
        {
            o.type = ExploreOutcomeType.Nothing;
            return o;
        }

        r -= W_NOTHING;
        if (r < W_REWARD)
        {
            o.type = ExploreOutcomeType.Reward;
            RollReward(ref o);
            return o;
        }

        o.type = ExploreOutcomeType.Danger;
        o.returnDelaySec = Random.Range(3f, 8f); // м'який штраф
        return o;
    }

    private void RollReward(ref ExploreOutcome o)
    {
        float total = W_WOOD + W_STONE + W_FISH;
        float r = Random.Range(0f, total);

        if (r < W_WOOD)
        {
            o.resourceId = "Wood";
            o.amount = Random.Range(2, 6); // 2..5
            return;
        }

        r -= W_WOOD;
        if (r < W_STONE)
        {
            o.resourceId = "Stone";
            o.amount = Random.Range(1, 5); // 1..4
            return;
        }

        o.resourceId = "Fish";
        o.amount = Random.Range(1, 4); // 1..3
    }
}
