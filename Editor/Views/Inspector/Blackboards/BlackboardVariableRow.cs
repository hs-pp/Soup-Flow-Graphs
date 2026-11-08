using System;
using Soup.Editor.Core.Commands;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Core.DataRefs;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector.Blackboards
{
    /// <summary>
    /// One blackboard variable: its name (double-click to rename), its type, and its default value.
    /// </summary>
    public class BlackboardVariableRow : VisualElement
    {
        private const string ROW_CLASS = "blackboardRow";
        private const string HEADER_CLASS = "blackboardRowHeader";
        private const string NAME_CLASS = "blackboardRowName";
        private const string NAME_FIELD_CLASS = "blackboardRowNameField";
        private const string TYPE_CLASS = "blackboardRowType";

        private readonly string m_variableId;
        private readonly Action<ICommand> m_requestCommand;
        private readonly Label m_nameLabel;
        private readonly TextField m_nameField;
        private bool m_isRenaming;

        public BlackboardVariableRow(BlackboardVariableRef variable, bool canMoveUp, bool canMoveDown, Action<ICommand> requestCommand)
        {
            m_variableId = variable.Id;
            m_requestCommand = requestCommand;
            AddToClassList(ROW_CLASS);

            VisualElement header = new();
            header.AddToClassList(HEADER_CLASS);
            Add(header);

            m_nameLabel = new Label(variable.Name) { tooltip = "Double-click to rename" };
            m_nameLabel.AddToClassList(NAME_CLASS);
            m_nameLabel.RegisterCallback<MouseDownEvent>(HandleNameMouseDown);
            header.Add(m_nameLabel);

            m_nameField = new TextField();
            m_nameField.AddToClassList(NAME_FIELD_CLASS);
            m_nameField.style.display = DisplayStyle.None;
            m_nameField.RegisterCallback<KeyDownEvent>(HandleNameKeyDown, TrickleDown.TrickleDown);
            m_nameField.RegisterCallback<FocusOutEvent>(HandleNameFocusOut);
            header.Add(m_nameField);

            Label typeLabel = new(EditorTypesFactory.GetBlackboardVariableDisplayName(variable.GetVariableType()));
            typeLabel.AddToClassList(TYPE_CLASS);
            header.Add(typeLabel);

            // On the header only, so the value field keeps its own menu.
            header.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Rename", _ => BeginRename());
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Move Up", _ => m_requestCommand(new MoveBlackboardVariableCommand(m_variableId, -1)),
                    canMoveUp ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Move Down", _ => m_requestCommand(new MoveBlackboardVariableCommand(m_variableId, 1)),
                    canMoveDown ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Delete", _ => m_requestCommand(new DeleteBlackboardVariableCommand(m_variableId)));
            }));

            SerializedProperty valueSP = variable.ValueSP;
            if (valueSP != null)
            {
                PropertyField valueField = new(valueSP, "Default");
                valueField.BindProperty(valueSP);
                Add(valueField);
            }
        }

        // While renaming, the field keeps what's being typed.
        public void SetName(string name)
        {
            m_nameLabel.text = name;
        }

        public void CancelRename()
        {
            EndRename(false);
        }

        private void HandleNameMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 0 && evt.clickCount == 2)
            {
                BeginRename();
                evt.StopPropagation();
            }
        }

        private void BeginRename()
        {
            if (m_isRenaming)
            {
                return;
            }

            m_isRenaming = true;
            m_nameField.SetValueWithoutNotify(m_nameLabel.text);
            m_nameLabel.style.display = DisplayStyle.None;
            m_nameField.style.display = DisplayStyle.Flex;

            // A hidden field can't take focus, so this waits for the field to show.
            m_nameField.schedule.Execute(() =>
            {
                m_nameField.Focus();
                m_nameField.SelectAll();
            });
        }

        private void HandleNameKeyDown(KeyDownEvent evt)
        {
            if (!m_isRenaming)
            {
                return;
            }

            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                EndRename(true);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                EndRename(false);
                evt.StopPropagation();
            }
        }

        // Leaving the field saves it. Focus can leave in the middle of a refresh, which can't run a command, so it waits a frame.
        private void HandleNameFocusOut(FocusOutEvent evt)
        {
            if (m_isRenaming)
            {
                schedule.Execute(() => EndRename(true));
            }
        }

        private void EndRename(bool commit)
        {
            if (!m_isRenaming)
            {
                return;
            }

            m_isRenaming = false;
            m_nameField.style.display = DisplayStyle.None;
            m_nameLabel.style.display = DisplayStyle.Flex;

            string name = m_nameField.value?.Trim();
            if (commit && !string.IsNullOrEmpty(name) && name != m_nameLabel.text)
            {
                m_requestCommand(new RenameBlackboardVariableCommand(m_variableId, name));
            }
        }
    }
}
