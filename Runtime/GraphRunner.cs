using System;
using Soup.Runtime.Blackboards;
using Soup.Runtime.BuiltInNodes;
using UnityEngine;

namespace Soup.Runtime
{
    /// <summary>
    /// Pure C# class for running a flow graph. Can only run a graph once. Create a fresh GraphRunner for every run.
    /// Also make sure to call UpdateGraph() on tick.
    /// </summary>
    public sealed class GraphRunner
    {
        private enum RunState
        {
            NotStarted,
            Running,
            Stopped,
        }

        public NodeCollection NodeCollection { get; }
        public AGraphProperties GraphProperties { get; }
        public Blackboard Blackboard { get; }
        public ANode ActiveNode { get; private set; }

        private RunState m_state = RunState.NotStarted;
        public bool IsRunning => m_state == RunState.Running;
        private NodeExecutionContext m_activeContext;
        
        private bool m_operationRunning;
        private bool m_stopRequested;
        
        /** The transition waiting for the next tick. Null when not transitioning, or equals STOP_GRAPH_EDGE if we're at the end of the graph.*/
        private OutportEdge m_pendingEdge;
        private static readonly OutportEdge STOP_GRAPH_EDGE = new();

        public Action OnGraphStart = null;
        public Action OnGraphStop = null;
        public Action<ANode> OnActiveNodeChanged = null;

        public GraphRunner(NodeCollection nodeCollection, AGraphProperties graphProperties, Blackboard blackboard)
        {
            NodeCollection = nodeCollection;
            GraphProperties = graphProperties;
            Blackboard = blackboard;
        }

        public void StartGraph()
        {
            if (m_state != RunState.NotStarted)
            {
                Debug.LogError("A GraphRunner runs once. Create a new one to run again.");
                return;
            }

            EntryNode entryNode = NodeCollection?.EntryNode;
            if (entryNode == null)
            {
                Debug.LogError("GraphRunner could not start because there is no entry node.");
                m_state = RunState.Stopped;
                return;
            }

            BeginOperation();
            m_state = RunState.Running;
            OnGraphStart?.Invoke();
            ActivateNode(entryNode);
            EndOperation();
        }

        public void UpdateGraph()
        {
            if (m_state != RunState.Running || m_operationRunning)
            {
                return;
            }

            BeginOperation();
            if (m_pendingEdge != null) // Handle the transition to next node and nothing else.
            {
                CommitPendingTransition();
            }
            else // Call node update
            {
                try
                {
                    ActiveNode.OnNodeUpdate(m_activeContext);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    m_stopRequested = true;
                }
            }
            EndOperation();
        }

        public void StopGraph()
        {
            if (m_state != RunState.Running)
            {
                return;
            }

            if (m_operationRunning)
            {
                m_stopRequested = true;
                return;
            }

            BeginOperation();
            EndRun();
            EndOperation();
        }

        private void RequestTransition(NodeExecutionContext context, OutportEdge edge)
        {
            if (context != m_activeContext)
            {
                Debug.LogError($"'{context.Node.GetType().Name}' requested a transition from an old context. Ignoring.");
                return;
            }

            if (m_pendingEdge != null)
            {
                Debug.LogWarning("A transition was already requested this tick. Ignoring this request.");
                return;
            }

            m_pendingEdge = edge;
            if (m_pendingEdge == null) // if passed in edge was null, we want to stop graph.
            {
                m_pendingEdge = STOP_GRAPH_EDGE;
            }
        }

        private void CommitPendingTransition()
        {
            ANode nextNode = FindTargetNode(m_pendingEdge);

            DeactivateNode();
            if (nextNode == null || m_stopRequested)
            {
                EndRun();
                return;
            }

            ActivateNode(nextNode);
        }

        private ANode FindTargetNode(OutportEdge edge)
        {
            string targetNodeId = edge?.ConnectedNodeId;
            if (string.IsNullOrEmpty(targetNodeId))
            {
                return null;
            }

            ANode node = NodeCollection.GetNodeById(targetNodeId);
            if (node == null)
            {
                Debug.LogError($"Transition target node '{targetNodeId}' was not found.");
            }

            return node;
        }

        private void ActivateNode(ANode node)
        {
            ActiveNode = node;
            OnActiveNodeChanged?.Invoke(ActiveNode);
            try
            {
                // We create a new context for every node we enter.
                m_activeContext = new NodeExecutionContext(node, GraphProperties, Blackboard, RequestTransition);
                node.OnNodeEnter(m_activeContext);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                m_stopRequested = true;
            }
        }

        private void DeactivateNode()
        {
            NodeExecutionContext context = m_activeContext;
            ActiveNode = null;
            m_activeContext = null;
            m_pendingEdge = null;

            if (context != null)
            {
                try
                {
                    context.Node.OnNodeExit(context);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    m_stopRequested = true;
                }
            }
        }

        private void EndRun()
        {
            DeactivateNode();
            m_state = RunState.Stopped;
            OnGraphStop?.Invoke();
        }

        private void BeginOperation()
        {
            m_operationRunning = true;
        }

        private void EndOperation()
        {
            m_operationRunning = false;
            if (m_stopRequested)
            {
                m_stopRequested = false;
                StopGraph();
            }
        }
    }
}
