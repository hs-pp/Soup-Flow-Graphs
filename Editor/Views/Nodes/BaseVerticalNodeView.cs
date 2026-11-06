using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes
{
    public class BaseVerticalNodeView : BaseNodeView
    {
        protected VisualElement TitleContainer { get; set; }
        protected VisualElement InputContainer { get; set; }
        protected VisualElement OutputContainer { get; set; }
        protected VisualElement ExtensionContainer { get; set; }

        protected override void OnInitializeView(VisualElement root)
        {
            // Input Container
            InputContainer = new() { pickingMode = PickingMode.Ignore };
            InputContainer.AddToClassList("nodeIoInput");
            root.Add(InputContainer);

            // Title Container
            TitleContainer = new() { pickingMode = PickingMode.Ignore };
            TitleContainer.AddToClassList("nodeTitle");
            root.Add(TitleContainer);

            // Extension Container
            ExtensionContainer = new() { pickingMode = PickingMode.Ignore };
            ExtensionContainer.AddToClassList("nodeExtension");
            root.Add(ExtensionContainer);

            // Output Container
            OutputContainer = new() { pickingMode = PickingMode.Ignore };
            OutputContainer.AddToClassList("nodeIoOutput");
            root.Add(OutputContainer);
        }

        protected override void AddPort(PortView port)
        {
            base.AddPort(port);
            if (port.Direction == Direction.Input)
            {
                InputContainer.Add(port);
            }
            else
            {
                OutputContainer.Add(port);
            }
        }
    }
}
