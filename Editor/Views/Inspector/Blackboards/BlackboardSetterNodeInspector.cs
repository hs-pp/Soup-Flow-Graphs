using System;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector.Blackboards
{
    /// <summary>
    /// Picking the variable goes through a command, since the setter has to change with it. The setter's own fields are plain
    /// bindings.
    /// </summary>
    [CustomNodeInspector(typeof(BlackboardSetterNode))]
    public class BlackboardSetterNodeInspector : ANodeInspector
    {
        protected override bool UseDefaultHeader => true;

        private VisualElement m_tracker;
        private BlackboardVariablePicker m_picker;
        private HelpBox m_helpBox;
        private VisualElement m_setterFieldsContainer;
        private string m_boundSetterPath;
        private string m_boundSetterTypename;
        private bool m_rebindSetterFields;

        protected override VisualElement CreateCustomLayout()
        {
            VisualElement layout = new();

            m_tracker = new VisualElement();
            layout.Add(m_tracker);

            m_picker = new BlackboardVariablePicker("Variable", EditorTypesFactory.HasBlackboardSetter, showTypes: true);
            m_picker.OnPicked += HandleVariablePicked;
            layout.Add(m_picker);

            m_helpBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            layout.Add(m_helpBox);

            m_setterFieldsContainer = new VisualElement();
            layout.Add(m_setterFieldsContainer);
            return layout;
        }

        protected override void OnSetNode(NodeRef nodeRef, NodeMetaDataRef metaDataRef)
        {
            // Picks, renames and undo all arrive after this node was set, so the inspector follows the graph from here on.
            m_tracker.Unbind();
            if (nodeRef.IsValid)
            {
                m_tracker.TrackSerializedObjectValue(nodeRef.NodeSP.serializedObject, _ => Refresh());
            }

            m_rebindSetterFields = true;
            Refresh();
        }

        private void Refresh()
        {
            if (!m_nodeRef.IsValid)
            {
                m_helpBox.style.display = DisplayStyle.None;
                BindSetterFields(null);
                return;
            }

            BlackboardRef blackboard = new(m_nodeRef.NodeSP.serializedObject);
            string variableId = new BlackboardKeyRef(m_nodeRef.NodeSP.FindPropertyRelative(BlackboardSetterNode.VARIABLE_FIELD_NAME)).VariableId;
            m_picker.Refresh(blackboard, variableId);

            SerializedProperty setterSP = m_nodeRef.NodeSP.FindPropertyRelative(BlackboardSetterNode.SETTER_FIELD_NAME);
            string problem = null;
            if (string.IsNullOrEmpty(variableId))
            {
                problem = "Pick a variable.";
            }
            else if (!blackboard.TryGetVariable(variableId, out BlackboardVariableRef variable))
            {
                problem = "The variable was deleted, or the node came from another graph. Pick another one.";
            }
            else
            {
                Type valueType = variable.GetValueType();
                ABlackboardSetter setter = setterSP?.managedReferenceValue as ABlackboardSetter;
                if (setter == null)
                {
                    problem = $"There is no setter for {EditorTypesFactory.GetBlackboardValueTypeDisplayName(valueType)} variables.";
                }
                else if (setter.ValueType != valueType)
                {
                    problem = $"The setter works on {EditorTypesFactory.GetBlackboardValueTypeDisplayName(setter.ValueType)}, but '{variable.Name}' holds {EditorTypesFactory.GetBlackboardValueTypeDisplayName(valueType)}. Pick the variable again.";
                }
            }

            m_helpBox.text = problem ?? string.Empty;
            m_helpBox.messageType = string.IsNullOrEmpty(variableId) ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            m_helpBox.style.display = problem != null ? DisplayStyle.Flex : DisplayStyle.None;

            BindSetterFields(setterSP);
        }

        // A swapped setter needs fresh fields: the old ones were built for the previous type's.
        private void BindSetterFields(SerializedProperty setterSP)
        {
            string path = setterSP?.propertyPath;
            string typename = setterSP?.managedReferenceFullTypename;
            if (!m_rebindSetterFields && path == m_boundSetterPath && typename == m_boundSetterTypename)
            {
                return;
            }

            m_rebindSetterFields = false;
            m_boundSetterPath = path;
            m_boundSetterTypename = typename;
            m_setterFieldsContainer.Unbind();
            m_setterFieldsContainer.Clear();
            if (!string.IsNullOrEmpty(typename))
            {
                SerializedPropertyUtils.AddVisibleChildFields(m_setterFieldsContainer, setterSP);
            }
        }

        private void HandleVariablePicked(string variableId)
        {
            if (m_nodeRef.IsValid)
            {
                RequestCommand(new SetBlackboardNodeVariableCommand(m_nodeRef.Id, variableId));
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
