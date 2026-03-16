public static class EconomicTreasuryContextFactory
{
    public static EconomicContext Create(
        TreasuryService treasury,
        string actorId = null,
        string reason = null)
    {
        return new EconomicContext
        {
            source = treasury,
            target = treasury,
            actorId = actorId ?? string.Empty,
            reason = reason ?? string.Empty
        };
    }
}