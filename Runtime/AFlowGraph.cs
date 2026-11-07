using Soup.Runtime.Blackboards;
using UnityEngine;

namespace Soup.Runtime
{
    /// <summary>
    /// The base flow graph class. This class holds the root NodeCollection, a GraphProperties instance, and a Blackboard.
    /// To define a flow graph type, inherit from AFlowGraph(TProperties). 
    /// </summary>
    public abstract class AFlowGraph : ScriptableObject
    {
        [SerializeField]
        private NodeCollection m_rootCollection = new();
        public NodeCollection RootCollection => m_rootCollection;

        /** Implemented by AFlowGraph(TProperties). Only grab this at runtime if you want the default values. */
        public abstract AGraphProperties GraphProperties { get; }

        [SerializeField]
        private Blackboard m_blackboard = new();
        public Blackboard Blackboard => m_blackboard;

        private protected AFlowGraph() { }

        /** For editor use. */
#if UNITY_EDITOR
        internal const string ROOT_COLLECTION_FIELD_NAME = nameof(m_rootCollection);
        internal const string BLACKBOARD_FIELD_NAME = nameof(m_blackboard);
        internal const string GRAPH_PROPERTIES_FIELD_NAME = "m_graphProperties";
#endif
    }

    /// <summary>
    /// The base class for graph types. TProperties is the data every graph of this type has. Instance-specific data can be defined in the Blackboard once the soup editor is open.
    /// A graph type with no properties can use EmptyGraphProperties.
    /// </summary>
    public abstract class AFlowGraph<TProperties> : AFlowGraph where TProperties : AGraphProperties, new()
    {
        [SerializeField]
        private TProperties m_graphProperties = new();
        public override AGraphProperties GraphProperties => m_graphProperties;
    }
}
