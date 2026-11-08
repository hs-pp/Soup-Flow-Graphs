using System;

namespace Soup.Runtime.BuiltInNodes
{
    /// <summary>
    /// The very first node the NodeCollection runs. Only one entry node may exist in a NodeCollection and it cannot
    /// be created or deleted. 
    /// </summary>
    [Serializable]
    public sealed class EntryNode : ANode
    {
        /** This is a very special ID that every entry node shares. */
        public const string ENTRY_NODE_ID = "_Entry_";

        public EntryNode()
        {
            m_id = ENTRY_NODE_ID;
        }

        public override void OnNodeEnter(NodeExecutionContext context)
        {
            context.TraverseEdge(DEFAULT_OUTPORT);
        }
    }
}
