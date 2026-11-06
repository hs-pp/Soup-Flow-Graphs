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
    /// <summary>
    /// The graph's blackboard, one row per element. Adding, renaming, reordering and deleting go through commands; default values
    /// are plain bindings, like graph properties.
    /// </summary>
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
        private readonly List<BlackboardElementRow> m_rows = new();

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

            // An element whose class is missing stays in the asset but isn't shown, like a node whose type is missing.
            List<BlackboardElementRef> elements = new();
            List<string> rowKeys = new();
            foreach (BlackboardElementRef element in blackboard.GetElements())
            {
                if (!element.IsMissingType)
                {
                    elements.Add(element);
                    rowKeys.Add($"{element.ElementSP.propertyPath}|{element.Id}|{element.ElementSP.managedReferenceFullTypename}");
                }
            }

            m_emptyLabel.style.display = blackboard.IsValid && elements.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // Rebinding steals focus mid-edit, and this runs after every change to the graph. Rows are only rebuilt when the list
            // itself changed. A rename relabels them in place.
            SerializedObject serializedObject = blackboard.BlackboardSP?.serializedObject;
            if (serializedObject == m_boundObject && IsBoundTo(rowKeys))
            {
                for (int i = 0; i < m_rows.Count; i++)
                {
                    m_rows[i].SetName(elements[i].Name);
                }

                return;
            }

            ClearRows();
            m_boundObject = serializedObject;
            m_boundRowKeys.AddRange(rowKeys);
            for (int i = 0; i < elements.Count; i++)
            {
                BlackboardElementRow row = new(elements[i], i > 0, i < elements.Count - 1, RequestCommand);
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
            // A name field removed mid-rename loses focus on the way out, which must not commit.
            foreach (BlackboardElementRow row in m_rows)
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
            foreach (Type elementType in EditorTypesFactory.GetBlackboardElementTypes())
            {
                Type type = elementType;
                menu.AddItem(new GUIContent(EditorTypesFactory.GetBlackboardElementDisplayName(type)), false, () => RequestCommand(new AddBlackboardElementCommand(type)));
            }

            menu.DropDown(m_addButton.worldBound);
        }

        private void RequestCommand(ICommand command)
        {
            OnCommandRequested?.Invoke(command);
        }
    }
}
