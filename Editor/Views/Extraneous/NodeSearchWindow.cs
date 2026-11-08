using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    public class NodeSearchWindow : EditorWindow
    {
        private static readonly Vector2 k_WindowSize = new(260f, 300f);

        private Type m_graphType;
        private Action<Type> m_onSelect;
        private List<NodeUtils.MenuEntry> m_allEntries;
        private readonly List<NodeUtils.MenuEntry> m_filtered = new();
        private TextField m_searchField;
        private ListView m_listView;

        public static void Open(Vector2 screenPosition, Type graphType, Action<Type> onSelect)
        {
            NodeSearchWindow window = CreateInstance<NodeSearchWindow>();
            window.m_graphType = graphType;
            window.m_onSelect = onSelect;
            window.ShowAsDropDown(new Rect(screenPosition, Vector2.zero), k_WindowSize);
        }

        private void CreateGUI()
        {
            m_allEntries = NodeUtils.GetMenuEntries(m_graphType);

            m_searchField = new TextField();
            m_searchField.RegisterValueChangedCallback(evt => Filter(evt.newValue));
            m_searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown);
            rootVisualElement.Add(m_searchField);

            m_listView = new ListView
            {
                fixedItemHeight = 20f,
                selectionType = SelectionType.Single,
                makeItem = () =>
                {
                    Label label = new();
                    label.style.unityTextAlign = TextAnchor.MiddleLeft;
                    label.style.paddingLeft = 6;
                    return label;
                },
                bindItem = (element, i) => ((Label)element).text = m_filtered[i].MenuPath,
            };
            m_listView.itemsSource = m_filtered;
            m_listView.itemsChosen += chosen =>
            {
                foreach (object item in chosen)
                {
                    Confirm((NodeUtils.MenuEntry)item);
                    return;
                }
            };
            m_listView.style.flexGrow = 1;
            rootVisualElement.Add(m_listView);

            Filter(string.Empty);

            // Focus the search field once the window is actually up.
            rootVisualElement.schedule.Execute(() => m_searchField.Focus());
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.DownArrow:
                    MoveSelection(1);
                    evt.StopPropagation();
                    break;
                case KeyCode.UpArrow:
                    MoveSelection(-1);
                    evt.StopPropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    int index = Mathf.Max(0, m_listView.selectedIndex);
                    if (index < m_filtered.Count)
                    {
                        Confirm(m_filtered[index]);
                    }

                    evt.StopPropagation();
                    break;
                case KeyCode.Escape:
                    Close();
                    evt.StopPropagation();
                    break;
            }
        }

        private void MoveSelection(int delta)
        {
            if (m_filtered.Count == 0)
            {
                return;
            }

            int index = Mathf.Clamp(m_listView.selectedIndex + delta, 0, m_filtered.Count - 1);
            m_listView.selectedIndex = index;
            m_listView.ScrollToItem(index);
        }

        private void Filter(string searchText)
        {
            m_filtered.Clear();
            foreach (NodeUtils.MenuEntry entry in m_allEntries)
            {
                if (string.IsNullOrEmpty(searchText)
                    || entry.MenuPath.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    m_filtered.Add(entry);
                }
            }

            m_listView.RefreshItems();
            m_listView.selectedIndex = m_filtered.Count > 0 ? 0 : -1;
        }

        private void Confirm(NodeUtils.MenuEntry entry)
        {
            Action<Type> onSelect = m_onSelect;
            Close();
            onSelect?.Invoke(entry.NodeType);
        }
    }
}
