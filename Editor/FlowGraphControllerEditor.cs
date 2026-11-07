using System;
using Soup.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Soup.Editor
{
    /// <summary>
    /// Picking a graph seeds the override with a copy of that graph's properties, so overrides always start from the asset's own
    /// values. The graph field is deliberately unbound: written by hand, the graph and its seeded override land in one undo step,
    /// and an undo that brings an earlier graph back can't be mistaken for a pick and reseed over the restored override.
    /// </summary>
    [CustomEditor(typeof(FlowGraphController))]
    public class FlowGraphControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty m_flowGraphProperty;
        private SerializedProperty m_useOverridesProperty;
        private SerializedProperty m_overridePropertiesProperty;

        private ObjectField m_graphField;
        private VisualElement m_useOverridesField;
        private VisualElement m_overridesBox;
        private HelpBox m_mismatchHelpBox;
        private VisualElement m_overridePropertiesContainer;
        private string m_boundOverrideTypename;

        public override VisualElement CreateInspectorGUI()
        {
            m_flowGraphProperty = serializedObject.FindProperty(FlowGraphController.FlowGraphFieldName);
            m_useOverridesProperty = serializedObject.FindProperty(FlowGraphController.UseOverridesFieldName);
            m_overridePropertiesProperty = serializedObject.FindProperty(FlowGraphController.OverridePropertiesFieldName);
            m_boundOverrideTypename = null;

            var root = new VisualElement();

            var scriptField = new PropertyField(serializedObject.FindProperty("m_Script"));
            scriptField.SetEnabled(false);
            root.Add(scriptField);

            m_graphField = new ObjectField(m_flowGraphProperty.displayName)
            {
                objectType = typeof(AFlowGraph),
                allowSceneObjects = false,
            };
            m_graphField.AddToClassList(ObjectField.alignedFieldUssClassName);
            m_graphField.RegisterValueChangedCallback(HandleGraphPicked);
            root.Add(m_graphField);

            m_useOverridesField = new PropertyField(m_useOverridesProperty);
            root.Add(m_useOverridesField);

            m_overridesBox = CreateOverridesBox();
            root.Add(m_overridesBox);

            var eventsContainer = new VisualElement { style = { marginTop = 8 } };
            eventsContainer.Add(new PropertyField(serializedObject.FindProperty(nameof(FlowGraphController.OnGraphStart))));
            eventsContainer.Add(new PropertyField(serializedObject.FindProperty(nameof(FlowGraphController.OnGraphStop))));
            eventsContainer.Add(new PropertyField(serializedObject.FindProperty(nameof(FlowGraphController.OnNodeChange))));
            root.Add(eventsContainer);

            // Undo, redo and other inspectors reach the unbound graph field and the override's visibility through here.
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            Refresh();
            return root;
        }

        private VisualElement CreateOverridesBox()
        {
            var box = new VisualElement
            {
                style =
                {
                    marginTop = 2, marginBottom = 2, marginLeft = 3,
                    paddingTop = 4, paddingBottom = 4, paddingLeft = 6, paddingRight = 4,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = new Color(0.1f, 0.1f, 0.1f), borderBottomColor = new Color(0.1f, 0.1f, 0.1f),
                    borderLeftColor = new Color(0.1f, 0.1f, 0.1f), borderRightColor = new Color(0.1f, 0.1f, 0.1f),
                    borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                },
            };

            m_mismatchHelpBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            box.Add(m_mismatchHelpBox);

            m_overridePropertiesContainer = new VisualElement();
            box.Add(m_overridePropertiesContainer);

            var resetButton = new Button(ResetOverridesToGraphDefaults) { text = "Reset to graph defaults" };
            resetButton.style.alignSelf = Align.FlexEnd;
            resetButton.style.marginTop = 4;
            box.Add(resetButton);

            return box;
        }

        private void HandleGraphPicked(ChangeEvent<Object> evt)
        {
            serializedObject.Update();
            m_flowGraphProperty.objectReferenceValue = evt.newValue;
            m_overridePropertiesProperty.managedReferenceValue = CopyGraphProperties(evt.newValue as AFlowGraph);
            serializedObject.ApplyModifiedProperties();
            Refresh();
        }

        private void ResetOverridesToGraphDefaults()
        {
            serializedObject.Update();
            m_overridePropertiesProperty.managedReferenceValue = CopyGraphProperties(m_flowGraphProperty.objectReferenceValue as AFlowGraph);
            serializedObject.ApplyModifiedProperties();
            Refresh();
        }

        private static AGraphProperties CopyGraphProperties(AFlowGraph graph)
        {
            return graph != null ? graph.GraphProperties?.Clone() : null;
        }

        private void Refresh()
        {
            if (m_graphField == null || serializedObject.targetObject == null)
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            var graph = m_flowGraphProperty.objectReferenceValue as AFlowGraph;
            m_graphField.SetValueWithoutNotify(graph);

            bool hasGraph = graph != null;
            m_useOverridesField.style.display = hasGraph ? DisplayStyle.Flex : DisplayStyle.None;
            m_overridesBox.style.display = hasGraph && m_useOverridesProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasGraph)
            {
                return;
            }

            Type graphPropertiesType = graph.GraphProperties?.GetType();
            Type overrideType = m_overridePropertiesProperty.managedReferenceValue?.GetType();
            bool mismatched = overrideType == null || overrideType != graphPropertiesType;
            m_mismatchHelpBox.text = overrideType == null
                ? $"There are no overrides for '{graph.name}'. Reset them to start from the graph's values."
                : $"These overrides are '{overrideType.Name}', but '{graph.name}' uses '{graphPropertiesType?.Name ?? "none"}'. The graph will run with its own values until they are reset.";
            m_mismatchHelpBox.style.display = mismatched ? DisplayStyle.Flex : DisplayStyle.None;

            // A swapped reference needs a fresh field: the old one was built for the previous type's fields.
            string overrideTypename = m_overridePropertiesProperty.managedReferenceFullTypename;
            if (overrideTypename == m_boundOverrideTypename)
            {
                return;
            }

            m_boundOverrideTypename = overrideTypename;
            m_overridePropertiesContainer.Unbind();
            m_overridePropertiesContainer.Clear();
            if (overrideType != null)
            {
                var overrideField = new PropertyField(m_overridePropertiesProperty);
                overrideField.BindProperty(m_overridePropertiesProperty);
                m_overridePropertiesContainer.Add(overrideField);
            }
        }
    }
}
