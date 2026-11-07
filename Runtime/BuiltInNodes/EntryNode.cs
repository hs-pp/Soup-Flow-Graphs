using System;

namespace Soup.Runtime.BuiltInNodes
{
    [Serializable]
    public sealed class EntryNode : ANode
    {
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
