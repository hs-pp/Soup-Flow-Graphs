using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Runtime.BuiltInNodes;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    [CustomNodeView(typeof(SubGraphNode))]
    [CustomNodeName("Sub Graph")]
    public class SubGraphNodeView : BaseHorizontalNodeView
    {
        private const string GLYPH_CLASS = "subGraphNodeGlyph";

        protected override void OnAddExtensionsContents(VisualElement extensionsContainer)
        {
            VisualElement glyph = new() { pickingMode = PickingMode.Ignore };
            glyph.AddToClassList(GLYPH_CLASS);
            extensionsContainer.Add(glyph);
        }

        protected override void BindManipulators()
        {
            base.BindManipulators();
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }
        
        private void OnMouseDown(MouseDownEvent e)
        {
            if (Graph == null)
            {
                return;
            }
            
            if (e.button == 0 && e.clickCount == 2)
            {
                Graph.RequestCommand(new EnterSubGraphCommand(Id));
            }
        }
    }
}