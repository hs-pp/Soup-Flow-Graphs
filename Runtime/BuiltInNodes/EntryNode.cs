using System;

namespace Soup.Runtime.BuiltInNodes
{
    [Serializable]
    [Outports(OUTPORT_NAME)]
    public sealed class EntryNode : ANode
    {
        public const string ENTRY_NODE_ID = "_Entry_";
        public const string OUTPORT_NAME = "Out";

        public EntryNode()
        {
            m_id = ENTRY_NODE_ID;
        }

        public override void OnNodeEnter(NodeExecutionContext context)
        {
            context.TraverseEdge(OUTPORT_NAME);
        }
    }
}
