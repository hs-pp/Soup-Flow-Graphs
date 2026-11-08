using System;
using System.Collections.Generic;
using Soup.Runtime.BuiltInNodes;
using UnityEngine;

namespace Soup.Runtime
{
    /// <summary>
    /// The main container for nodes in a flow graph.
    /// </summary>
    [Serializable]
    public sealed class NodeCollection : ISerializationCallbackReceiver
    {
        [SerializeReference]
        private List<ANode> m_nodes = new();

        /** All node collections are guaranteed to have an EntryNode. */
        [SerializeReference]
        private EntryNode m_entryNode = new();
        public EntryNode EntryNode => m_entryNode;

        /** Only used at runtime for quick node lookup. */
        [NonSerialized]
        private Dictionary<string, ANode> m_nodeCacheById;

        public ANode GetNodeById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            
            if (m_nodeCacheById == null)
            {
                BuildNodeCache();
            }

            m_nodeCacheById.TryGetValue(id, out ANode node);
            return node;
        }

        private void BuildNodeCache()
        {
            m_nodeCacheById = new Dictionary<string, ANode>(m_nodes.Count + 1);

            // The entry node is not in m_nodes but is still addressable by id.
            if (!string.IsNullOrEmpty(m_entryNode.Id))
            {
                m_nodeCacheById.Add(m_entryNode.Id, m_entryNode);
            }

            foreach (ANode node in m_nodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(node.Id) && !m_nodeCacheById.TryAdd(node.Id, node))
                {
                    Debug.LogError($"Duplicate node id '{node.Id}' found.");
                }
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        /// <summary>
        /// Any deserialize (editor apply, undo, reimport, etc) can add, remove or replace nodes, so the lookup needs to be rebuilt.
        /// </summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_nodeCacheById = null;
        }

        /** For editor use. */
#if UNITY_EDITOR
        internal IReadOnlyList<ANode> Nodes => m_nodes;
        internal const string NODES_FIELD_NAME = nameof(m_nodes);
        internal const string ENTRY_NODE_FIELD_NAME = nameof(m_entryNode);
#endif
    }
}
