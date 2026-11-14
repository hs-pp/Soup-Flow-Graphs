using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Runtime.BuiltInNodes;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    [CustomNodeView(typeof(SubGraphNode))]
    [CustomNodeName("Sub Graph")]
    public class SubGraphNodeView : BaseHorizontalNodeView
    {
        private const string ICON_CLASS = "subGraphNodeIcon";

        protected override void OnAddExtensionsContents(VisualElement extensionsContainer)
        {
            VisualElement icon = new() { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(ICON_CLASS);
            extensionsContainer.Add(icon);
        }

        protected override void AddManipulators()
        {
            base.AddManipulators();
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
                e.StopPropagation();
            }
        }
    }
}