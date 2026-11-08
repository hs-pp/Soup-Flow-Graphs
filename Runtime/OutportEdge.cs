using System;
using UnityEngine;

namespace Soup.Runtime
{
    /// <summary>
    /// Unique outport definition within the scope of a node type. Definitions are used in ANode.
    /// </summary>
    public readonly struct OutportDefinition
    {
        public readonly string Id;
        public readonly string Name;

        public OutportDefinition(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    /// <summary>
    /// This class represents an outgoing port on a node.
    /// If the ConnectedNodeId is empty, we can infer that the outport exists but a connection out of it does not.
    /// 
    /// These node connections are singly linked. InportEdge as a class does not exist.
    /// </summary>
    [Serializable]
    public sealed class OutportEdge
    {
        [SerializeField]
        private string m_outportId;
        public string OutportId => m_outportId;

        [SerializeField]
        private string m_connectedNodeId;
        public string ConnectedNodeId => m_connectedNodeId;

        /** For editor use. */
#if UNITY_EDITOR
        internal void SetConnectedNodeId(string nodeId)
        {
            m_connectedNodeId = nodeId;
        }

        internal const string OUTPORT_ID_FIELD_NAME = nameof(m_outportId);
        internal const string CONNECTED_NODE_ID_FIELD_NAME = nameof(m_connectedNodeId);
#endif
    }
}