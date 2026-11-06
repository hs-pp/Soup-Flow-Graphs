using Soup.Runtime.BuiltInNodes;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    [CustomNodeView(typeof(EntryNode))]
    [CustomNodeName("Entry")]
    public class EntryNodeView : BaseHorizontalNodeView
    {
        protected override PortView CreateInputPort() => null; // Nothing connects into the entry node.
        protected override float MinWidth => 80f;
    }
}
