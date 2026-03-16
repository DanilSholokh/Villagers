using System;
using UnityEngine;

[Serializable]
public class ResourceReward
{
    [SerializeField] private ResourceBundle bundle = new();

    public ResourceBundle Bundle => bundle;

    public bool IsEmpty => bundle == null || bundle.IsEmpty;

    public ResourceReward()
    {
    }

    public ResourceReward(ResourceBundle bundle)
    {
        this.bundle = bundle ?? new ResourceBundle();
    }
}