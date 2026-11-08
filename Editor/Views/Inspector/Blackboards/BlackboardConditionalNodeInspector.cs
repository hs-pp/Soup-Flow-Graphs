using System;
using System.Collections.Generic;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector.Blackboards
{
    /// <summary>
    /// The variable and the conditions, in the order they are tested. Picking the variable and adding, moving or removing a
    /// condition go through commands, since each changes the node's outports; a condition's own fields are plain bindings.
    /// </summary>
    [CustomNodeInspector(typeof(BlackboardConditionalNode))]
    public class BlackboardConditionalNodeInspector : ANodeInspector
    {
        private const string NO_CONDITION_LABEL = "(no condition)";
        private static readonly Color s_entryBackground = new(0.23f, 0.23f, 0.23f);
        private static readonly Color s_entryBorder = new(0.14f, 0.14f, 0.14f);

        protected override bool UseDefaultHeader => true;

        private VisualElement m_tracker;
        private BlackboardVariablePicker m_picker;
        private HelpBox m_helpBox;
        private VisualElement m_entriesContainer;
        private Button m_addButton;
        private readonly List<Label> m_entryLabels = new();
        private readonly List<string> m_boundEntryKeys = new();
        private bool m_rebindEntries;

        protected override VisualElement CreateCustomLayout()
        {
            VisualElement layout = new();

            m_tracker = new VisualElement();
            layout.Add(m_tracker);

            m_picker = new BlackboardVariablePicker("Variable", EditorTypesFactory.HasBlackboardConditional, showTypes: true);
            m_picker.OnPicked += HandleVariablePicked;
            layout.Add(m_picker);

            m_helpBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            layout.Add(m_helpBox);

            Label caption = new("Conditions") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6, marginLeft = 3 } };
            layout.Add(caption);

            m_entriesContainer = new VisualElement();
            layout.Add(m_entriesContainer);

            m_addButton = new Button(HandleAddClicked) { text = "Add Condition" };
            m_addButton.style.alignSelf = Align.FlexEnd;
            layout.Add(m_addButton);

            Label elseNote = new("Else is taken when no condition holds.")
            {
                style = { unityFontStyleAndWeight = FontStyle.Italic, color = new Color(0.6f, 0.6f, 0.6f), marginLeft = 3, marginTop = 2 },
            };
            layout.Add(elseNote);
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

            m_rebindEntries = true;
            Refresh();
        }

        private void Refresh()
        {
            if (!m_nodeRef.IsValid)
            {
                m_helpBox.style.display = DisplayStyle.None;
                BindEntries(null, null);
                return;
            }

            BlackboardRef blackboard = BlackboardRef.Find(m_nodeRef.NodeSP.serializedObject);
            string variableId = BlackboardInspectorUtils.GetVariableId(m_nodeRef, BlackboardConditionalNode.VARIABLE_FIELD_NAME);
            m_picker.Refresh(blackboard, variableId);

            SerializedProperty entriesSP = m_nodeRef.NodeSP.FindPropertyRelative(BlackboardConditionalNode.ENTRIES_FIELD_NAME);
            string problem = BlackboardInspectorUtils.GetVariableProblem(blackboard, variableId, out BlackboardVariableRef variable, out Type valueType);
            bool canAdd = problem == null && EditorTypesFactory.HasBlackboardConditional(valueType);
            if (problem == null && !canAdd)
            {
                problem = $"There are no conditions for {EditorTypesFactory.GetBlackboardValueTypeDisplayName(valueType)} variables.";
            }
            else if (problem == null && !AllConditionsWorkOn(entriesSP, valueType))
            {
                problem = $"Some conditions work on another type than '{variable.Name}'. They take Else when the graph runs. Pick the variable again to clear them.";
            }

            m_helpBox.text = problem ?? string.Empty;
            m_helpBox.messageType = string.IsNullOrEmpty(variableId) ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            m_helpBox.style.display = problem != null ? DisplayStyle.Flex : DisplayStyle.None;
            m_addButton.SetEnabled(canAdd);

            BindEntries(entriesSP, valueType);
        }

        // Rebinding steals focus mid-edit, so entries are only rebuilt when the list changed. Edits to a condition relabel it.
        private void BindEntries(SerializedProperty entriesSP, Type valueType)
        {
            List<string> entryKeys = new();
            for (int i = 0; i < (entriesSP?.arraySize ?? 0); i++)
            {
                SerializedProperty entrySP = entriesSP.GetArrayElementAtIndex(i);
                entryKeys.Add($"{entrySP.propertyPath}|{GetEntryId(entrySP)}|{GetConditionalSP(entrySP).managedReferenceFullTypename}");
            }

            if (!m_rebindEntries && IsBoundTo(entryKeys))
            {
                for (int i = 0; i < m_entryLabels.Count; i++)
                {
                    m_entryLabels[i].text = GetEntryLabel(entriesSP.GetArrayElementAtIndex(i), i, valueType);
                }

                return;
            }

            m_rebindEntries = false;
            m_boundEntryKeys.Clear();
            m_boundEntryKeys.AddRange(entryKeys);
            m_entryLabels.Clear();
            m_entriesContainer.Unbind();
            m_entriesContainer.Clear();
            for (int i = 0; i < entryKeys.Count; i++)
            {
                m_entriesContainer.Add(CreateEntryRow(entriesSP.GetArrayElementAtIndex(i), i, entryKeys.Count, valueType));
            }
        }

        private bool IsBoundTo(List<string> entryKeys)
        {
            if (entryKeys.Count != m_boundEntryKeys.Count)
            {
                return false;
            }

            for (int i = 0; i < entryKeys.Count; i++)
            {
                if (entryKeys[i] != m_boundEntryKeys[i])
                {
                    return false;
                }
            }

            return true;
        }

        private VisualElement CreateEntryRow(SerializedProperty entrySP, int index, int count, Type valueType)
        {
            string entryId = GetEntryId(entrySP);
            VisualElement row = new()
            {
                style =
                {
                    marginTop = 2, marginBottom = 2, marginLeft = 3,
                    paddingTop = 3, paddingBottom = 3, paddingLeft = 6, paddingRight = 4,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = s_entryBorder, borderBottomColor = s_entryBorder, borderLeftColor = s_entryBorder, borderRightColor = s_entryBorder,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                    backgroundColor = s_entryBackground,
                },
            };

            VisualElement header = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(header);

            Label label = new(GetEntryLabel(entrySP, index, valueType)) { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } };
            header.Add(label);
            m_entryLabels.Add(label);

            header.Add(CreateEntryButton("▲", "Move up", index > 0, () => RequestCommand(new MoveBlackboardConditionCommand(m_nodeRef.Id, entryId, -1))));
            header.Add(CreateEntryButton("▼", "Move down", index < count - 1, () => RequestCommand(new MoveBlackboardConditionCommand(m_nodeRef.Id, entryId, 1))));
            header.Add(CreateEntryButton("✕", "Remove, with its outport and connection", true, () => RequestCommand(new RemoveBlackboardConditionCommand(m_nodeRef.Id, entryId))));

            SerializedPropertyUtils.AddVisibleChildFields(row, GetConditionalSP(entrySP));
            return row;
        }

        private static Button CreateEntryButton(string text, string tooltip, bool enabled, Action clicked)
        {
            Button button = new(clicked)
            {
                text = text,
                tooltip = tooltip,
                style = { width = 20, height = 18, paddingLeft = 0, paddingRight = 0, fontSize = 9 },
            };
            button.SetEnabled(enabled);
            return button;
        }

        private static string GetEntryLabel(SerializedProperty entrySP, int index, Type valueType)
        {
            ABlackboardConditional conditional = GetConditionalSP(entrySP).managedReferenceValue as ABlackboardConditional;
            string label = conditional?.GetLabel() ?? NO_CONDITION_LABEL;
            bool mismatched = conditional == null || (valueType != null && conditional.ValueType != valueType);
            return $"{index + 1}.  {label}{(mismatched ? "  ⚠" : string.Empty)}";
        }

        private static bool AllConditionsWorkOn(SerializedProperty entriesSP, Type valueType)
        {
            for (int i = 0; i < entriesSP.arraySize; i++)
            {
                if ((GetConditionalSP(entriesSP.GetArrayElementAtIndex(i)).managedReferenceValue as ABlackboardConditional)?.ValueType != valueType)
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetEntryId(SerializedProperty entrySP)
        {
            return entrySP.FindPropertyRelative(BlackboardConditionalEntry.OUTPORT_ID_FIELD_NAME).stringValue;
        }

        private static SerializedProperty GetConditionalSP(SerializedProperty entrySP)
        {
            return entrySP.FindPropertyRelative(BlackboardConditionalEntry.CONDITIONAL_FIELD_NAME);
        }

        private void HandleVariablePicked(string variableId)
        {
            if (m_nodeRef.IsValid)
            {
                RequestCommand(new SetBlackboardNodeVariableCommand(m_nodeRef.Id, variableId));
            }
        }

        private void HandleAddClicked()
        {
            if (m_nodeRef.IsValid)
            {
                RequestCommand(new AddBlackboardConditionCommand(m_nodeRef.Id));
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
