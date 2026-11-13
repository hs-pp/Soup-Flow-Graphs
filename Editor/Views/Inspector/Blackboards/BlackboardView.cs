using System;
using System.Collections.Generic;
using Soup.Editor.Core.Commands;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.Core.DataRefs;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector.Blackboards
{
    [UxmlElement]
    public partial class BlackboardView : VisualElement
    {
        private const string UXML_PATH = "Soup/BlackboardView";
        private const string ADD_BUTTON_TAG = "add-button";
        private const string EMPTY_LABEL_TAG = "empty-label";
        private const string ROWS_TAG = "rows";

        private Button m_addButton;
        private Label m_emptyLabel;
        private VisualElement m_rowsContainer;
        private readonly List<BlackboardVariableRow> m_rows = new();

        private SerializedObject m_boundObject;
        private readonly List<string> m_boundRowKeys = new();

        public event Action<ICommand> OnCommandRequested;

        public BlackboardView()
        {
            CreateLayout();
        }

        private void CreateLayout()
        {
            var uxmlAsset = Resources.Load<VisualTreeAsset>(UXML_PATH);
            uxmlAsset.CloneTree(this);

            m_addButton = this.Q<Button>(ADD_BUTTON_TAG);
            m_emptyLabel = this.Q<Label>(EMPTY_LABEL_TAG);
            m_rowsContainer = this.Q(ROWS_TAG);

            m_addButton.clicked += ShowAddMenu;
        }

        public void SetBlackboard(BlackboardRef blackboard)
        {
            m_addButton.SetEnabled(blackboard.IsValid);

            List<BlackboardVariableRef> variables = new();
            List<string> rowKeys = new();
            foreach (BlackboardVariableRef variable in blackboard.GetVariables())
            {
                if (!variable.IsMissingType)
                {
                    variables.Add(variable);
                    rowKeys.Add($"{variable.VariableSP.propertyPath}|{variable.Id}|{variable.VariableSP.managedReferenceFullTypename}");
                }
            }

            m_emptyLabel.style.display = blackboard.IsValid && variables.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            SerializedObject serializedObject = blackboard.BlackboardSP?.serializedObject;
            if (serializedObject == m_boundObject && IsBoundTo(rowKeys))
            {
                for (int i = 0; i < m_rows.Count; i++)
                {
                    m_rows[i].SetName(variables[i].Name);
                }

                return;
            }

            ClearRows();
            m_boundObject = serializedObject;
            m_boundRowKeys.AddRange(rowKeys);
            for (int i = 0; i < variables.Count; i++)
            {
                BlackboardVariableRow row = new(variables[i], i > 0, i < variables.Count - 1, RequestCommand);
                m_rows.Add(row);
                m_rowsContainer.Add(row);
            }
        }

        private bool IsBoundTo(List<string> rowKeys)
        {
            if (rowKeys.Count != m_boundRowKeys.Count)
            {
                return false;
            }

            for (int i = 0; i < rowKeys.Count; i++)
            {
                if (rowKeys[i] != m_boundRowKeys[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void ClearRows()
        {
            foreach (BlackboardVariableRow row in m_rows)
            {
                row.CancelRename();
            }

            m_rowsContainer.Unbind();
            m_rowsContainer.Clear();
            m_rows.Clear();
            m_boundRowKeys.Clear();
            m_boundObject = null;
        }

        private void ShowAddMenu()
        {
            GenericMenu menu = new();
            foreach (Type variableType in EditorTypesFactory.GetBlackboardVariableTypes())
            {
                Type type = variableType;
                menu.AddItem(new GUIContent(EditorTypesFactory.GetBlackboardVariableDisplayName(type)), false, () => RequestCommand(new AddBlackboardVariableCommand(type)));
            }

            menu.DropDown(m_addButton.worldBound);
        }

        private void RequestCommand(ICommand command)
        {
            OnCommandRequested?.Invoke(command);
        }
    }
}
