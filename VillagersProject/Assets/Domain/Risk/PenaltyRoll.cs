using UnityEngine;

public static class PenaltyRoll
{
    // riskTier 0..5 -> ваги (None/Lost/Death)
    // Мінімально, потім відбалансимо.
    private static readonly float[] W_NONE = { 0.95f, 0.85f, 0.75f, 0.60f, 0.45f, 0.30f };
    private static readonly float[] W_LOST = { 0.05f, 0.12f, 0.18f, 0.25f, 0.35f, 0.45f };
    private static readonly float[] W_DEATH = { 0.00f, 0.03f, 0.07f, 0.15f, 0.20f, 0.25f };

    public static PenaltyType RollPenalty(int riskTier)
    {
        int t = Mathf.Clamp(riskTier, 0, 5);
        float total = W_NONE[t] + W_LOST[t] + W_DEATH[t];
        float r = Random.Range(0f, total);

        if (r < W_NONE[t]) return PenaltyType.None;
        r -= W_NONE[t];

        if (r < W_LOST[t]) return PenaltyType.Lost;
        return PenaltyType.Death;
    }

    // hidden outcome для Lost (AliveLost / DeadLost)
    public static LostHiddenOutcome RollLostHidden(int riskTier)
    {
        int t = Mathf.Clamp(riskTier, 0, 5);

        // Чим більший tier — тим більше шанс DeadLost
        float deadChance = Mathf.Lerp(0.10f, 0.55f, t / 5f);
        return (Random.value < deadChance) ? LostHiddenOutcome.DeadLost : LostHiddenOutcome.AliveLost;
    }
}