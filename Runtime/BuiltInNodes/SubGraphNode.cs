using System;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes
{
    /// <summary>
    /// Runs a subgraph which is stored as a separate node collection inside this node.
    /// </summary>
    [Serializable]
    [CustomNode(typeof(AFlowGraph), "Flow/Sub Graph")]
    public sealed class SubGraphNode : ANode
    {
        private sealed class SubGraphNodeRuntimeState : NodeRuntimeState
        {
            public GraphRunner ChildRunner;
        }
        
        [SerializeField]
        private NodeCollection m_subGraph = new();
        public NodeCollection SubGraph => m_subGraph;
        
        public override NodeRuntimeState CreateRuntimeState() => new SubGraphNodeRuntimeState();

        public override void OnNodeEnter(NodeExecutionContext context)
        {
            SubGraphNodeRuntimeState state = (SubGraphNodeRuntimeState)context.RuntimeState;
            state.ChildRunner = new GraphRunner(m_subGraph, context.GraphProperties, context.Blackboard);
            state.ChildRunner.StartGraph();
        }

        public override void OnNodeUpdate(NodeExecutionContext context)
        {
            SubGraphNodeRuntimeState state = (SubGraphNodeRuntimeState)context.RuntimeState;
            state.ChildRunner.UpdateGraph();
            
            if (!state.ChildRunner.IsRunning) // It must have finished. Leave node.
            {
                context.TraverseEdge(DEFAULT_OUTPORT);
            }
        }

        public override void OnNodeExit(NodeExecutionContext context)
        {
            SubGraphNodeRuntimeState state = (SubGraphNodeRuntimeState)context.RuntimeState;
            state.ChildRunner?.StopGraph();
        }

        /** For editor use. */
#if UNITY_EDITOR
        internal const string SUBGRAPH_FIELD_NAME = nameof(m_subGraph);
#endif
    }
}
