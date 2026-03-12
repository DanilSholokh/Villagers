using UnityEngine;

public enum SurveyOutcomeType
{
    Nothing = 0,
    RevealHiddenResource = 1,
    BonusGold = 2,
    Danger = 3
}

public struct SurveyOutcome
{
    public SurveyOutcomeType type;
    public int goldAmount;
}

public class SurveyOutcomeService
{
    private const float W_NOTHING = 0.40f;
    private const float W_REVEAL = 0.35f;
    private const float W_GOLD = 0.15f;
    private const float W_DANGER = 0.10f;

    public SurveyOutcome Roll(LocationModel location)
    {
        var o = new SurveyOutcome();

        float revealWeight = W_REVEAL;
        if (location == null || location.potentialResources == null || location.potentialResources.Count == 0)
            revealWeight = 0f;

        float total = W_NOTHING + revealWeight + W_GOLD + W_DANGER;
        float r = Random.Range(0f, total);

        if (r < W_NOTHING)
        {
            o.type = SurveyOutcomeType.Nothing;
            return o;
        }

        r -= W_NOTHING;
        if (r < revealWeight)
        {
            o.type = SurveyOutcomeType.RevealHiddenResource;
            return o;
        }

        r -= revealWeight;
        if (r < W_GOLD)
        {
            o.type = SurveyOutcomeType.BonusGold;
            o.goldAmount = Random.Range(1, 4);
            return o;
        }

        o.type = SurveyOutcomeType.Danger;
        return o;
    }
}