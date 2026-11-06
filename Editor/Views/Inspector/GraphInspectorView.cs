using System;
using Soup.Editor.Core;
using Soup.Editor.Core.Commands;
using Soup.Editor.Core.DataRefs;
using Soup.Editor.Views.Inspector.Blackboards;
using Soup.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    [UxmlElement]
    public partial class GraphInspectorView : VisualElement
    {
        private const string UXML_PATH = "Soup/GraphInspectorView";
        private const string TITLE_TAG = "title";
        private const string TYPE_LABEL_TAG = "type-label";
        private const string PING_ICON_TAG = "ping-icon";
        private const string PROPERTIES_CONTAINER_TAG = "properties-container";
        private const string BLACKBOARD_VIEW_TAG = "blackboard-view";

        private Label m_titleLabel;
        private Label m_typeLabel;
        private Image m_pingIcon;
        private VisualElement m_propertiesContainer;
        private BlackboardView m_blackboardView;

        public event Action<ICommand> OnCommandRequested;

        private AFlowGraph m_graph;
        private SerializedObject m_boundObject;
        private string m_boundPath;

        public GraphInspectorView()
        {
            CreateLayout();
        }

        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            m_titleLabel = this.Q<Label>(TITLE_TAG);
            m_typeLabel = this.Q<Label>(TYPE_LABEL_TAG);
            m_pingIcon = this.Q<Image>(PING_ICON_TAG);
            m_propertiesContainer = this.Q(PROPERTIES_CONTAINER_TAG);
            m_blackboardView = this.Q<BlackboardView>(BLACKBOARD_VIEW_TAG);

            m_blackboardView.OnCommandRequested += command => OnCommandRequested?.Invoke(command);
            m_pingIcon.RegisterCallback<ClickEvent>(_ =>
            {
                if (m_graph != null)
                {
                    EditorGUIUtility.PingObject(m_graph);
                }
            });
        }

        public void SetGraph(LoadedFlowGraph loadedFlowGraph)
        {
            // Labels are cheap and a rename only arrives as a reload, so they refresh on every call.
            m_graph = loadedFlowGraph != null && loadedFlowGraph.IsGraphAlive ? loadedFlowGraph.GraphAsset : null;
            m_titleLabel.text = m_graph != null ? m_graph.name : string.Empty;
            m_typeLabel.text = m_graph != null ? m_graph.GetType().Name : string.Empty;
            m_pingIcon.image = m_graph != null ? AssetPreview.GetMiniThumbnail(m_graph) : null;

            BlackboardRef blackboard = default;
            loadedFlowGraph?.TryGetBlackboard(out blackboard);
            m_blackboardView.SetBlackboard(blackboard);

            SerializedProperty graphProperties = null;
            loadedFlowGraph?.TryGetGraphProperties(out graphProperties);

            // Rebinding steals focus mid-edit, and this runs after every change to the graph.
            if (graphProperties != null && graphProperties.serializedObject == m_boundObject && graphProperties.propertyPath == m_boundPath)
            {
                return;
            }

            m_propertiesContainer.Unbind();
            m_propertiesContainer.Clear();
            m_boundObject = null;
            m_boundPath = null;

            if (graphProperties == null)
            {
                return;
            }

            m_boundObject = graphProperties.serializedObject;
            m_boundPath = graphProperties.propertyPath;

            var propertyField = new PropertyField(graphProperties);
            propertyField.BindProperty(graphProperties);
            m_propertiesContainer.Add(propertyField);
        }
    }
}
