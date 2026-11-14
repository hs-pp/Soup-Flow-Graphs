using System;
using System.Collections.Generic;
using Soup.Runtime.BuiltInNodes;
using UnityEngine;

namespace Soup.Editor.State
{
    /// <summary>
    /// Hidden as a subasset to the FlowGraph .asset instance.
    /// We ensure there are no direct references to it so it'll be auto-stripped from builds.
    /// </summary>
    public sealed class FlowGraphMetaData : ScriptableObject
    {
        [SerializeField]
        private NodeCollectionMetaData m_rootCollection = new();

        public const string ROOT_COLLECTION_FIELD_NAME = nameof(m_rootCollection);
    }

    [Serializable]
    public sealed class NodeCollectionMetaData
    {
        [SerializeReference]
        private List<NodeMetaData> m_nodes = new();
        public IReadOnlyList<NodeMetaData> Nodes => m_nodes;

        [SerializeField]
        private List<GroupMetaData> m_groups = new();
        public IReadOnlyList<GroupMetaData> Groups => m_groups;

        public NodeCollectionMetaData()
        {
            // Every collection has exactly one entry node, so its meta data exists from the start.
            m_nodes.Add(new EntryNodeMetaData { NodeId = EntryNode.ENTRY_NODE_ID });
        }

        public const string NODES_FIELD_NAME = nameof(m_nodes);
        public const string GROUPS_FIELD_NAME = nameof(m_groups);
    }
    
    [Serializable]
    public class NodeMetaData
    {
        [SerializeField, HideInInspector]
        private string m_nodeId;
        public string NodeId { get => m_nodeId; set => m_nodeId = value; }

        [SerializeField]
        private Vector2 m_position;
        public Vector2 Position { get => m_position; set => m_position = value; }

        [SerializeField]
        private Color m_color = Color.gray3;
        public Color Color { get => m_color; set => m_color = value; }

        [SerializeField]
        private string m_comment;
        public string Comment { get => m_comment; set => m_comment = value; }
        
        public virtual void CopyFrom(NodeMetaData source)
        {
            if (source == null)
            {
                return;
            }

            m_nodeId = source.m_nodeId;
            m_position = source.m_position;
            m_color = source.m_color;
            m_comment = source.m_comment;
        }

        public const string NODE_ID_FIELD_NAME = nameof(m_nodeId);
        public const string POSITION_FIELD_NAME = nameof(m_position);
        public const string COLOR_FIELD_NAME = nameof(m_color);
        public const string COMMENT_FIELD_NAME = nameof(m_comment);
    }
    
    // These are built-in meta data's that do not require the CustomNodeMetaData attribute.
    [Serializable]
    public sealed class EntryNodeMetaData : NodeMetaData
    {
        public static readonly Vector2 DEFAULT_POSITION = new(100f, 100f);

        public EntryNodeMetaData()
        {
            Position = DEFAULT_POSITION;
        }
    }

    [Serializable]
    public sealed class SubGraphNodeMetaData : NodeMetaData
    {
        [SerializeField]
        private NodeCollectionMetaData m_subGraph = new();
        public NodeCollectionMetaData SubGraph => m_subGraph;

        public const string SUBGRAPH_FIELD_NAME = nameof(m_subGraph);
    }

    [Serializable]
    public sealed class GroupMetaData
    {
        public const string DEFAULT_TITLE = "Group";
        public static readonly Vector2 DEFAULT_SIZE = new(600f, 300f);
        public static readonly Vector2 MIN_SIZE = new(200f, 100f);
        public static Vector2 ClampSize(Vector2 size) => Vector2.Max(size, MIN_SIZE);
        public static readonly Color DEFAULT_COLOR = new(0.2f, 0.22f, 0.27f, 0.8f);

        [SerializeField, HideInInspector]
        private string m_groupId;
        public string GroupId => m_groupId;

        [SerializeField]
        private string m_title;
        public string Title => m_title;

        [SerializeField]
        private Rect m_rect;
        public Rect Rect => m_rect;

        [SerializeField]
        private Color m_color;
        public Color Color => m_color;

        public const string GROUP_ID_FIELD_NAME = nameof(m_groupId);
        public const string TITLE_FIELD_NAME = nameof(m_title);
        public const string RECT_FIELD_NAME = nameof(m_rect);
        public const string COLOR_FIELD_NAME = nameof(m_color);
    }
}
