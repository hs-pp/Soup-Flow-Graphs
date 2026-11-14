using System;
using Soup.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Soup.Editor
{
    [CustomEditor(typeof(FlowGraphController))]
    public class FlowGraphControllerEditor : UnityEditor.Editor
    {
        private const string STYLESHEET_PATH = "Soup/FlowGraphControllerEditor";
        private const string OVERRIDES_BOX_CLASS = "flowGraphControllerOverridesBox";
        private const string RESET_BUTTON_CLASS = "flowGraphControllerResetButton";
        private const string EVENTS_CLASS = "flowGraphControllerEvents";

        private SerializedProperty m_flowGraphSP;
        private SerializedProperty m_useOverridesSP;
        private SerializedProperty m_overridePropertiesSP;

        private ObjectField m_graphField;
        private VisualElement m_useOverridesField;
        private VisualElement m_overridesBox;
        private HelpBox m_helpBox;
        private VisualElement m_overridePropertiesContainer;
        private string m_boundOverrideTypename;

        public override VisualElement CreateInspectorGUI()
        {
            m_flowGraphSP = serializedObject.FindProperty(FlowGraphController.FLOW_GRAPH_FIELD_NAME);
            m_useOverridesSP = serializedObject.FindProperty(FlowGraphController.USE_OVERRIDES_FIELD_NAME);
            m_overridePropertiesSP = serializedObject.FindProperty(FlowGraphController.OVERRIDE_PROPERTIES_FIELD_NAME);
            m_boundOverrideTypename = null;

            var root = new VisualElement();
            root.styleSheets.Add(Resources.Load<StyleSheet>(STYLESHEET_PATH));

            var scriptField = new PropertyField(serializedObject.FindProperty("m_Script"));
            scriptField.SetEnabled(false);
            root.Add(scriptField);

            m_graphField = new ObjectField(m_flowGraphSP.displayName)
            {
                objectType = typeof(AFlowGraph),
                allowSceneObjects = false,
            };
            m_graphField.AddToClassList(ObjectField.alignedFieldUssClassName);
            m_graphField.RegisterValueChangedCallback(HandleGraphPicked);
            root.Add(m_graphField);

            m_useOverridesField = new PropertyField(m_useOverridesSP);
            root.Add(m_useOverridesField);

            m_overridesBox = CreateOverridesBox();
            root.Add(m_overridesBox);

            root.Add(new PropertyField(serializedObject.FindProperty(FlowGraphController.RUN_ON_START_FIELD_NAME)));

            var eventsContainer = new VisualElement();
            eventsContainer.AddToClassList(EVENTS_CLASS);
            eventsContainer.Add(new PropertyField(serializedObject.FindProperty(nameof(FlowGraphController.OnGraphStart))));
            eventsContainer.Add(new PropertyField(serializedObject.FindProperty(nameof(FlowGraphController.OnGraphStop))));
            eventsContainer.Add(new PropertyField(serializedObject.FindProperty(nameof(FlowGraphController.OnActiveNodeChange))));
            root.Add(eventsContainer);

            // Undo, redo and other inspectors reach the unbound graph field and the override's visibility through here.
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            Refresh();
            return root;
        }

        private VisualElement CreateOverridesBox()
        {
            var box = new VisualElement();
            box.AddToClassList(OVERRIDES_BOX_CLASS);

            m_helpBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            box.Add(m_helpBox);

            m_overridePropertiesContainer = new VisualElement();
            box.Add(m_overridePropertiesContainer);

            var resetButton = new Button(ResetOverridesToGraphDefaults) { text = "Reset to graph defaults" };
            resetButton.AddToClassList(RESET_BUTTON_CLASS);
            box.Add(resetButton);

            return box;
        }

        private void HandleGraphPicked(ChangeEvent<Object> evt)
        {
            serializedObject.Update();
            m_flowGraphSP.objectReferenceValue = evt.newValue;
            m_overridePropertiesSP.managedReferenceValue = CopyGraphProperties(evt.newValue as AFlowGraph);
            serializedObject.ApplyModifiedProperties();
            Refresh();
        }

        private void ResetOverridesToGraphDefaults()
        {
            serializedObject.Update();
            m_overridePropertiesSP.managedReferenceValue = CopyGraphProperties(m_flowGraphSP.objectReferenceValue as AFlowGraph);
            serializedObject.ApplyModifiedProperties();
            Refresh();
        }

        private static AGraphProperties CopyGraphProperties(AFlowGraph graph)
        {
            return graph != null ? graph.GraphProperties?.Clone() : null;
        }

        private void Refresh()
        {
            if (serializedObject.targetObject == null)
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            var graph = m_flowGraphSP.objectReferenceValue as AFlowGraph;
            m_graphField.SetValueWithoutNotify(graph);
            BindOverrideFields();

            bool hasGraph = graph != null;
            m_useOverridesField.style.display = hasGraph ? DisplayStyle.Flex : DisplayStyle.None;
            m_overridesBox.style.display = hasGraph && m_useOverridesSP.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasGraph)
            {
                return;
            }

            Type graphPropertiesType = graph.GraphProperties?.GetType();
            Type overrideType = m_overridePropertiesSP.managedReferenceValue?.GetType();
            bool mismatched = overrideType == null || overrideType != graphPropertiesType;
            if (overrideType == null)
            {
                m_helpBox.text = $"There are no overrides for '{graph.name}'. Reset them to start from the graph's values.";
            }
            else
            {
                string graphPropertiesTypeName = "none";
                if (graphPropertiesType != null)
                {
                    graphPropertiesTypeName = graphPropertiesType.Name;
                }

                m_helpBox.text = $"These overrides are '{overrideType.Name}', but '{graph.name}' uses '{graphPropertiesTypeName}'. The graph will run with its own values until they are reset.";
            }

            m_helpBox.style.display = mismatched ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // A swapped reference needs a fresh field: the old one was built for the previous type's fields.
        private void BindOverrideFields()
        {
            string overrideTypename = m_overridePropertiesSP.managedReferenceFullTypename;
            if (overrideTypename == m_boundOverrideTypename)
            {
                return;
            }

            m_boundOverrideTypename = overrideTypename;
            m_overridePropertiesContainer.Unbind();
            m_overridePropertiesContainer.Clear();
            if (m_overridePropertiesSP.managedReferenceValue != null)
            {
                var overrideField = new PropertyField(m_overridePropertiesSP);
                overrideField.BindProperty(m_overridePropertiesSP);
                m_overridePropertiesContainer.Add(overrideField);
            }
        }
    }
}
