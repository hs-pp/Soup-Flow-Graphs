using System;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes
{
    [Serializable]
    [NodeMenu("Flow/Sub Graph")]
    public sealed class SubGraphNode : ANode
    {
        [SerializeField]
        private NodeCollection m_subGraph = new();
        public NodeCollection SubGraph => m_subGraph;

        public override NodeRuntimeState CreateRuntimeState()
        {
            return new SubGraphNodeRuntimeState();
        }

        public override void OnNodeEnter(NodeExecutionContext context)
        {
            SubGraphNodeRuntimeState state = context.GetNodeRuntimeState<SubGraphNodeRuntimeState>();
            if (state == null || m_subGraph == null)
            {
                context.StopGraph();
                return;
            }

            state.IsComplete = false;
            state.ChildRunner = new GraphRunner(m_subGraph, context.GraphProperties, context.Blackboard);
            state.ChildRunner.OnGraphStop += () => state.IsComplete = true;
            state.ChildRunner.StartGraph();

            if (!state.ChildRunner.IsRunning)
            {
                state.IsComplete = true;
            }
        }

        public override void OnNodeUpdate(NodeExecutionContext context)
        {
            SubGraphNodeRuntimeState state = context.GetNodeRuntimeState<SubGraphNodeRuntimeState>();
            if (state?.ChildRunner == null)
            {
                context.StopGraph();
                return;
            }

            if (!state.IsComplete && state.ChildRunner.IsRunning)
            {
                state.ChildRunner.UpdateGraph();
            }

            if (state.IsComplete || !state.ChildRunner.IsRunning)
            {
                state.IsComplete = true;
                context.TraverseEdge(DefaultOutport);
            }
        }

        public override void OnNodeExit(NodeExecutionContext context)
        {
            SubGraphNodeRuntimeState state = context.GetNodeRuntimeState<SubGraphNodeRuntimeState>();
            if (state?.ChildRunner != null)
            {
                state.ChildRunner.StopGraph();
                state.ChildRunner = null;
                state.IsComplete = false;
            }
        }
        
#if UNITY_EDITOR
        internal const string SubGraphFieldName = nameof(m_subGraph);
#endif
    }

    public sealed class SubGraphNodeRuntimeState : NodeRuntimeState
    {
        public GraphRunner ChildRunner;
        public bool IsComplete;
    }
}
