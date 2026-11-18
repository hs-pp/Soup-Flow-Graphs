using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    [CustomNodeInspector(typeof(SubGraphNode))]
    public class SubGraphNodeInspector : ANodeInspector
    {
        private const string UXML_PATH = "Soup/SubGraphNodeInspector";
        private const string NAME_FIELD_TAG = "name-field";
        private const string PREVIEW_TAG = "preview";
        private const string NODE_COUNT_TAG = "node-count";

        protected override bool UseDefaultHeader => true;

        private VisualElement m_nodeTracker;
        private VisualElement m_metaDataTracker;
        private TextField m_nameField;
        private SubGraphPreviewElement m_preview;
        private Label m_nodeCountLabel;

        protected override VisualElement CreateCustomLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            VisualElement layout = uxmlAsset.Instantiate();

            m_nodeTracker = new VisualElement();
            m_metaDataTracker = new VisualElement();
            layout.Add(m_nodeTracker);
            layout.Add(m_metaDataTracker);
            m_nameField = layout.Q<TextField>(NAME_FIELD_TAG);
            m_preview = layout.Q<SubGraphPreviewElement>(PREVIEW_TAG);
            m_nodeCountLabel = layout.Q<Label>(NODE_COUNT_TAG);

            m_preview.OnClicked += HandlePreviewClicked;
            return layout;
        }

        protected override void OnSetNode(NodeRef nodeRef, NodeMetaDataRef metaDataRef)
        {
            m_nodeTracker.Unbind();
            m_metaDataTracker.Unbind();
            m_nameField.Unbind();
            SerializedProperty nameSP = nodeRef.NodeSP?.FindPropertyRelative(SubGraphNode.NAME_FIELD_NAME);
            if (nameSP != null)
            {
                m_nameField.BindProperty(nameSP);
            }
            if (nodeRef.TryGetSubGraphCollection(out NodeCollectionRef collection))
            {
                m_nodeTracker.TrackPropertyValue(collection.CollectionSP, _ => Refresh());
            }
            if (metaDataRef.TryGetSubGraphCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData))
            {
                m_metaDataTracker.TrackPropertyValue(collectionMetaData.CollectionSP, _ => Refresh());
            }

            Refresh();
        }

        private void Refresh()
        {
            m_nodeRef.TryGetSubGraphCollection(out NodeCollectionRef collection);
            m_metaDataRef.TryGetSubGraphCollectionMetaData(out NodeCollectionMetaDataRef collectionMetaData);

            m_preview.SetContents(collection, collectionMetaData);

            if (!collection.IsValid)
            {
                m_nodeCountLabel.text = "-";
                return;
            }

            m_nodeCountLabel.text = collection.NodesSP.arraySize.ToString();
        }

        private void HandlePreviewClicked()
        {
            if (m_nodeRef.IsValid && !string.IsNullOrEmpty(m_nodeRef.Id))
            {
                RequestCommand(new EnterSubGraphCommand(m_nodeRef.Id));
            }
        }

        protected override void HandleLoadState(string saveState)
        {
        }

        protected override string GetSaveState()
        {
            return "";
        }
    }
}
