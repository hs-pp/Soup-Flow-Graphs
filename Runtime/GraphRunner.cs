using System;
using System.Collections.Generic;
using Soup.Runtime.Blackboards;
using UnityEngine;

namespace Soup.Runtime
{
    public class GraphRunner
    {
        private struct PendingTransition
        {
            public ANode SourceNode;
            public OutportEdge Edge;
            public bool Exists;
        }
        
        private enum NodeStage
        {
            None,
            Enter,
            Update,
            Exit,
        }
        
        private NodeCollection m_nodeCollection = null;
        private readonly Dictionary<string, NodeRuntimeState> m_nodeRuntimeStates = new ();
        public AGraphProperties GraphProperties { get; private set; }
        public Blackboard Blackboard { get; private set; }
        public ANode CurrentNode => m_currentNode;
        public bool IsRunning => m_currentNode != null;
        private PendingTransition m_pendingTransition;
        private NodeStage m_currentStage = NodeStage.None;

        [NonSerialized]
        public Action OnGraphStart = null;
        [NonSerialized]
        public Action OnGraphStop = null;
        [NonSerialized]
        public Action<ANode> OnNodeChange = null;
        private ANode m_currentNode = null;

        public GraphRunner(AFlowGraph flowGraph, AGraphProperties graphProperties, Blackboard blackboard)
        {
            m_nodeCollection = flowGraph.RootCollection;
            GraphProperties = graphProperties;
            Blackboard = blackboard;

            if (m_nodeCollection == null)
            {
                Debug.LogError("No Graph attached to GraphRunner!");
            }
        }

        public GraphRunner(NodeCollection nodeCollection, AGraphProperties graphProperties, Blackboard blackboard)
        {
            m_nodeCollection = nodeCollection;
            GraphProperties = graphProperties;
            Blackboard = blackboard;
        }

        public void StartGraph()
        {
            m_pendingTransition = default;
            m_nodeRuntimeStates.Clear();
            m_currentNode = null;

            if (m_nodeCollection == null)
            {
                Debug.LogError("GraphRunner could not start because no node collection was provided.");
                OnNodeChange?.Invoke(null);
                return;
            }

            m_currentNode = m_nodeCollection.EntryNode;
            if (m_currentNode == null)
            {
                Debug.LogError("GraphRunner could not start because the node collection has no entry node.");
                OnNodeChange?.Invoke(null);
                return;
            }

            OnGraphStart?.Invoke();
            OnNodeChange?.Invoke(m_currentNode);
            ExecuteNodeCallback(m_currentNode, NodeStage.Enter, node => node.OnNodeEnter(CreateNodeExecutionContext(node)));
        }

        public void StopGraph()
        {
            if (m_currentNode == null)
            {
                return;
            }

            ExecuteNodeCallback(m_currentNode, NodeStage.Exit, node => node.OnNodeExit(CreateNodeExecutionContext(node)));
            Stop();
        }

        public void UpdateGraph()
        {
            if (m_currentNode == null)
            {
                return;
            }

            if (m_pendingTransition.Exists)
            {
                CommitPendingTransition();
            }
            else
            {
                ExecuteNodeCallback(m_currentNode, NodeStage.Update, node => node.OnNodeUpdate(CreateNodeExecutionContext(node)));
            }
        }

        private void RequestTransition(ANode node, OutportEdge edge)
        {
            switch (m_currentStage)
            {
                case NodeStage.None:
                    Debug.LogError("Transitions can only be requested from a node callback.");
                    return;

                case NodeStage.Exit:
                    // The graph has already committed to where it is going;
                    // a node on its way out cannot redirect it. Without this
                    // guard the request outlives the exiting node, suppresses
                    // the next node's own transition, and then fires from the
                    // wrong node on the following tick.
                    Debug.LogError($"'{node.GetType().Name}' requested a transition from OnNodeExit. Transitions can only be requested from OnNodeEnter or OnNodeUpdate.");
                    return;
            }

            if (node != m_currentNode)
            {
                Debug.LogError($"'{node.GetType().Name}' requested a transition but is not the current node.");
                return;
            }

            // A null edge is the stop-the-graph sentinel from NodeExecutionContext.StopGraph.
            if (edge != null && m_currentNode.GetOutportEdge(edge.Id) == null)
            {
                Debug.LogError($"Outport '{edge.Id}' does not belong to '{node.GetType().Name}'.");
                return;
            }

            if (m_pendingTransition.Exists)
            {
                Debug.LogWarning("A transition was already requested during this node callback. Ignoring the later request.");
                return;
            }

            m_pendingTransition = new PendingTransition
            {
                SourceNode = node,
                Edge = edge,
                Exists = true
            };
        }

        private void CommitPendingTransition()
        {
            if (!m_pendingTransition.Exists || m_currentNode == null)
            {
                return;
            }

            if (m_pendingTransition.SourceNode != m_currentNode)
            {
                Debug.LogError("Pending transition belongs to a node that is no longer current. Discarding it.");
                m_pendingTransition = default;
                return;
            }

            PendingTransition transition = m_pendingTransition;
            m_pendingTransition = default;

            ExecuteNodeCallback(m_currentNode, NodeStage.Exit, node => node.OnNodeExit(CreateNodeExecutionContext(node)));
            
            string targetNodeId = transition.Edge?.ConnectedNodeId;
            if (string.IsNullOrEmpty(targetNodeId)) // we're done.
            {
                Stop();
                return;
            }

            ANode nextNode = m_nodeCollection.GetNodeById(targetNodeId);
            if (nextNode == null)
            {
                Debug.LogError($"Transition target node '{targetNodeId}' was not found.");
                Stop();
                return;
            }

            m_currentNode = nextNode;
            OnNodeChange?.Invoke(m_currentNode);
            ExecuteNodeCallback(m_currentNode, NodeStage.Enter, node => node.OnNodeEnter(CreateNodeExecutionContext(node)));
        }

        private void Stop()
        {
            m_currentNode = null;
            m_pendingTransition = default;
            OnGraphStop?.Invoke();
            OnNodeChange?.Invoke(null);
        }

        private NodeExecutionContext CreateNodeExecutionContext(ANode node)
        {
            if (node == null)
            {
                return null;
            }

            // Get node state from dictionary
            if (!m_nodeRuntimeStates.TryGetValue(node.Id, out NodeRuntimeState state))
            {
                state = node.CreateRuntimeState() ?? new DefaultNodeRuntimeState();
                m_nodeRuntimeStates.Add(node.Id, state);
            }
            
            return new NodeExecutionContext(node, GraphProperties, Blackboard, state, RequestTransition);
        }

        private void ExecuteNodeCallback(ANode node, NodeStage stage, Action<ANode> callback)
        {
            if (node == null)
            {
                return;
            }

            NodeStage previous = m_currentStage;
            m_currentStage = stage;

            try
            {
                callback.Invoke(node);
            }
            finally
            {
                // Ensure we always get here.
                m_currentStage = previous; // This should basically always be CallbackPhase.None, but this is probably safer?
            }
        }
    }
}
