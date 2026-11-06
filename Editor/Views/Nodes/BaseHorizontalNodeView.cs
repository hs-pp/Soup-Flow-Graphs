using Soup.Editor.Core.DataRefs;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes
{
    public class BaseHorizontalNodeView : BaseNodeView
    {
        private const string UXML_PATH = "Soup/BaseHorizontalNodeView";
        private const string TITLE_CONTAINER_TAG = "title-container";
        private const string TITLE_LABEL_TAG = "title-label";
        private const string INPUT_CONTAINER_TAG = "input-container";
        private const string OUTPUT_CONTAINER_TAG = "output-container";
        private const string EXTENSION_CONTAINER_TAG = "extension-container";

        protected VisualElement TitleContainer { get; set; }
        protected VisualElement InputContainer { get; set; }
        protected VisualElement OutputContainer { get; set; }
        protected VisualElement ExtensionContainer { get; set; }
        
        protected Label m_titleLabel;
        
        protected virtual void OnAddExtensionsContents(VisualElement extensionsContainer) { }
        
        protected override void OnInitializeView(VisualElement root)
        {
            CreateLayout(root);
            OnAddExtensionsContents(ExtensionContainer);
            ExtensionContainer.style.display = ExtensionContainer.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void CreateLayout(VisualElement root)
        {
            VisualTreeAsset uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(root);

            TitleContainer = this.Q<VisualElement>(TITLE_CONTAINER_TAG);
            m_titleLabel =  this.Q<Label>(TITLE_LABEL_TAG);
            InputContainer = this.Q<VisualElement>(INPUT_CONTAINER_TAG);
            OutputContainer = this.Q<VisualElement>(OUTPUT_CONTAINER_TAG);
            ExtensionContainer = this.Q<VisualElement>(EXTENSION_CONTAINER_TAG);
        }

        protected override void OnPropertyBound(NodeRef nodeRef, NodeMetaDataRef metaDataRef)
        {
            base.OnPropertyBound(nodeRef, metaDataRef);
            m_titleLabel.text = EditorTypesFactory.GetNodeDisplayName(NodeType);
        }

        protected override void OnMetaDataChanged(NodeMetaDataRef metaData)
        {
            TitleContainer.style.backgroundColor = metaData.Color;
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