using System;
using Soup.Runtime.Blackboards;
using UnityEngine;

namespace Soup.Runtime
{
    /// <summary>
    /// The graph asset. Graph classes derive from AFlowGraph&lt;TProperties&gt;, never from this directly, so every graph has
    /// properties.
    /// </summary>
    [Serializable]
    public abstract class AFlowGraph : ScriptableObject
    {
        [SerializeField]
        private NodeCollection m_rootCollection = new NodeCollection();
        public NodeCollection NodeCollection => m_rootCollection;

        [SerializeField]
        private Blackboard m_blackboard = new Blackboard();

        /// <summary>
        /// This asset's own variables. Runs work on a copy, so these are the defaults every run starts from.
        /// </summary>
        public Blackboard Blackboard => m_blackboard;

        /// <summary>
        /// The graph's properties as their base type. AFlowGraph&lt;TProperties&gt;.Properties has them typed.
        /// </summary>
        public abstract AGraphProperties GraphProperties { get; }

        // Only AFlowGraph<TProperties> can call this, so a graph class outside this assembly can't skip declaring its properties.
        private protected AFlowGraph()
        {
        }

#if UNITY_EDITOR
        internal const string RootCollectionFieldName = nameof(m_rootCollection);
        internal const string BlackboardFieldName = nameof(m_blackboard);
        internal const string GraphPropertiesFieldName = "m_graphProperties"; // Declared by AFlowGraph<TProperties>.
#endif
    }

    /// <summary>
    /// A graph class with its properties class. Declare a graph as <c>public sealed class DialogueGraph : AFlowGraph&lt;DialogueGraphProperties&gt; { }</c>.
    /// </summary>
    [Serializable]
    public abstract class AFlowGraph<TProperties> : AFlowGraph where TProperties : AGraphProperties, new()
    {
        // A plain field rather than [SerializeReference]: it is never null or the wrong type, and renaming the properties class
        // keeps its data.
        [SerializeField]
        private TProperties m_graphProperties = new TProperties();
        public TProperties Properties => m_graphProperties;

        public override AGraphProperties GraphProperties => m_graphProperties;
    }
}
