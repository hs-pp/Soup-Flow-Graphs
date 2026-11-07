using System;
using UnityEngine;

namespace Soup.Runtime
{
    /// <summary>
    /// Inherit from this to define class properties for your graph type.
    /// </summary>
    [Serializable]
    public abstract class AGraphProperties
    {
        public AGraphProperties Clone() => JsonUtility.FromJson(JsonUtility.ToJson(this), GetType()) as AGraphProperties;
    }

    /// <summary>
    /// Empty GraphProperties to use for any Graph that doesn't need properties.
    /// </summary>
    [Serializable]
    public sealed class EmptyGraphProperties : AGraphProperties
    {
    }
}
