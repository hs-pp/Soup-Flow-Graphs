using System;
using Soup.Runtime.Blackboards;
using UnityEngine;

namespace Soup.Runtime
{
    public sealed class NodeExecutionContext
    {
        private readonly Action<NodeExecutionContext, OutportEdge> m_requestTransition;

        public ANode Node { get; }
        public NodeRuntimeState RuntimeState { get; }
        public AGraphProperties GraphProperties { get; }
        public Blackboard Blackboard { get; } // The run's own copy.

        public NodeExecutionContext(ANode node, AGraphProperties graphProperties, Blackboard blackboard, Action<NodeExecutionContext, OutportEdge> requestTransition)
        {
            Node = node;
            RuntimeState = node.CreateRuntimeState() ?? new EmptyNodeRuntimeState();
            GraphProperties = graphProperties;
            Blackboard = blackboard;
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

            m_requestTransition.Invoke(this, edge);
        }

        public void StopGraph()
        {
            m_requestTransition.Invoke(this, null);
        }
    }
}
