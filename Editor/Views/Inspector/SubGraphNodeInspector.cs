using System.Collections.Generic;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    [CustomNodeInspector(typeof(SubGraphNode))]
    public class SubGraphNodeInspector : ANodeInspector
    {
        private const string UXML_PATH = "Soup/SubGraphNodeInspector";
        private const string PREVIEW_TAG = "preview";
        private const string NODE_COUNT_TAG = "node-count";
        private const string CONNECTION_COUNT_TAG = "connection-count";
        private const string GROUP_COUNT_TAG = "group-count";
        private const string SUBGRAPH_COUNT_TAG = "subgraph-count";

        protected override bool UseDefaultHeader => true;

        private VisualElement m_nodeTracker;
        private VisualElement m_metaDataTracker;
        private SubGraphPreviewElement m_preview;
        private Label m_nodeCountLabel;
        private Label m_connectionCountLabel;
        private Label m_groupCountLabel;
        private Label m_subGraphCountLabel;

        protected override VisualElement CreateCustomLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            VisualElement layout = uxmlAsset.Instantiate();

            m_nodeTracker = new VisualElement();
            m_metaDataTracker = new VisualElement();
            layout.Add(m_nodeTracker);
            layout.Add(m_metaDataTracker);
            m_preview = layout.Q<SubGraphPreviewElement>(PREVIEW_TAG);
            m_nodeCountLabel = layout.Q<Label>(NODE_COUNT_TAG);
            m_connectionCountLabel = layout.Q<Label>(CONNECTION_COUNT_TAG);
            m_groupCountLabel = layout.Q<Label>(GROUP_COUNT_TAG);
            m_subGraphCountLabel = layout.Q<Label>(SUBGRAPH_COUNT_TAG);

            m_preview.OnClicked += HandlePreviewClicked;
            return layout;
        }

        protected override void OnSetNode(NodeRef nodeRef, NodeMetaDataRef metaDataRef)
        {
            m_nodeTracker.Unbind();
            m_metaDataTracker.Unbind();
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
                m_connectionCountLabel.text = "-";
                m_groupCountLabel.text = "-";
                m_subGraphCountLabel.text = "-";
                return;
            }

            HashSet<string> nodeIds = new(collection.GetNodesById().Keys);
            int connectionCount = 0;
            int subGraphCount = 0;
            foreach (NodeRef node in collection.GetNodes())
            {
                if (!node.IsEntryNode && node.TryGetSubGraphCollection(out _))
                {
                    subGraphCount++;
                }

                foreach (OutportRef outport in node.EnumerateOutports())
                {
                    if (IsConnected(outport, nodeIds))
                    {
                        connectionCount++;
                    }
                }
            }

            // The entry node is implicit, so it isn't counted.
            m_nodeCountLabel.text = collection.NodesSP.arraySize.ToString();
            m_connectionCountLabel.text = connectionCount.ToString();
            m_groupCountLabel.text = collectionMetaData.IsValid ? collectionMetaData.GroupsSP.arraySize.ToString() : "-";
            m_subGraphCountLabel.text = subGraphCount.ToString();
        }

        private static bool IsConnected(OutportRef outport, HashSet<string> nodeIds)
        {
            string targetId = outport.ConnectedNodeId;
            return !string.IsNullOrEmpty(targetId) && nodeIds.Contains(targetId);
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
