using System;

[Serializable]
public class EconomicContext
{
    public IResourceContainer source;
    public IResourceContainer target;

    public string actorId;
    public string reason;
}