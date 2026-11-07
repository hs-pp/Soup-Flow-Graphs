using System;
using Soup.Runtime.Blackboards;
using UnityEngine;

namespace Soup.Runtime
{
    public sealed class NodeExecutionContext
    {
        private readonly NodeRuntimeState m_runtimeState;
        private readonly Action<ANode, OutportEdge> m_requestTransition;
        
        public ANode Node { get; }
        public NodeRuntimeState RuntimeState => m_runtimeState;
        public AGraphProperties GraphProperties { get; }
        public Blackboard Blackboard { get; } // The run's own copy.

        public NodeExecutionContext(ANode node, AGraphProperties graphProperties, Blackboard blackboard, NodeRuntimeState runtimeState, Action<ANode, OutportEdge> requestTransition)
        {
            Node = node;
            GraphProperties = graphProperties;
            Blackboard = blackboard;
            m_runtimeState = runtimeState;
            m_requestTransition = requestTransition;
        }

        public T GetGraphProperties<T>() where T : AGraphProperties
        {
            return GraphProperties as T;
        }

        public T GetNodeRuntimeState<T>() where T : NodeRuntimeState
        {
            return RuntimeState as T;
        }

        public void TraverseEdge(string outportId)
        {
            OutportEdge edge = Node.GetOutportEdge(outportId);
            if (edge == null)
            {
                Debug.LogError($"'{Node.GetType().Name}' has no outport '{outportId}'. It must match an outport id from DefineOutports. Stopping the graph.");
                StopGraph();
                return;
            }

            m_requestTransition.Invoke(Node, edge);
        }

        public void StopGraph()
        {
            m_requestTransition.Invoke(Node, null);
        }
    }
}
