using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    public class NodeSearchWindow : EditorWindow
    {
        private const string STYLESHEET_PATH = "Soup/NodeSearchWindow";
        private const string STYLECLASS_NAME = "nodeSearchWindow";
        private const string SEARCH_FIELD_CLASS = "nodeSearchField";
        private const string HEADER_CLASS = "nodeSearchHeader";
        private const string BACK_ARROW_CLASS = "nodeSearchBackArrow";
        private const string TITLE_CLASS = "nodeSearchTitle";
        private const string ROWS_CLASS = "nodeSearchRows";
        private const string ROW_CLASS = "nodeSearchRow";
        private const string ROW_NAME_CLASS = "nodeSearchRowName";
        private const string ROW_PATH_CLASS = "nodeSearchRowPath";
        private const string ROW_ARROW_CLASS = "nodeSearchRowArrow";
        private const string HIGHLIGHTED_ROW_CLASS = "nodeSearchRowHighlighted";

        private const string ROOT_TITLE = "Create Node";
        private const string SEARCH_TITLE = "Search";

        private const float WINDOW_OFFSET_Y = 16f;
        private static readonly Vector2 WINDOW_SIZE = new(260f, 320f);

        private class Page
        {
            public readonly string Title;
            public readonly Page Parent;
            public readonly List<Page> Folders = new();
            public readonly List<NodeUtils.MenuEntry> Entries = new();

            /** The highlighted row. Folders come first, then entries. It's kept so that going back returns to it. */
            public int HighlightedIndex;

            public Page(string title, Page parent)
            {
                Title = title;
                Parent = parent;
            }
        }

        private Type m_graphType;
        private Action<Type> m_onSelect;
        private List<NodeUtils.MenuEntry> m_entries;

        private Page m_folderPage;
        private Page m_page;

        private TextField m_searchField;
        private Label m_backArrow;
        private Label m_titleLabel;
        private ScrollView m_rows;

        private bool IsSearching => !string.IsNullOrEmpty(m_searchField.value);

        public static void Open(Vector2 screenPosition, Type graphType, Action<Type> onSelect)
        {
            NodeSearchWindow window = CreateInstance<NodeSearchWindow>();
            window.m_graphType = graphType;
            window.m_onSelect = onSelect;

            Rect anchor = new(screenPosition.x - WINDOW_SIZE.x / 2f, screenPosition.y - WINDOW_OFFSET_Y, WINDOW_SIZE.x, 0f);
            window.ShowAsDropDown(anchor, WINDOW_SIZE);
        }

        private void CreateGUI()
        {
            rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>(STYLESHEET_PATH));
            rootVisualElement.AddToClassList(STYLECLASS_NAME);

            // A click on anything that can't take focus would take it from the search field.
            rootVisualElement.RegisterCallback<PointerDownEvent>(evt => rootVisualElement.focusController.IgnoreEvent(evt), TrickleDown.TrickleDown);

            m_searchField = new TextField();
            m_searchField.textEdition.placeholder = "Search";
            m_searchField.AddToClassList(SEARCH_FIELD_CLASS);
            m_searchField.RegisterValueChangedCallback(evt => ShowSearchResults(evt.newValue));
            m_searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.Add(m_searchField);

            VisualElement header = new();
            header.AddToClassList(HEADER_CLASS);
            header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            rootVisualElement.Add(header);

            m_backArrow = new Label("‹");
            m_backArrow.AddToClassList(BACK_ARROW_CLASS);
            header.Add(m_backArrow);

            m_titleLabel = new Label();
            m_titleLabel.AddToClassList(TITLE_CLASS);
            header.Add(m_titleLabel);

            m_rows = new ScrollView();
            m_rows.AddToClassList(ROWS_CLASS);
            rootVisualElement.Add(m_rows);

            m_entries = NodeUtils.GetMenuEntries(m_graphType);
            m_folderPage = BuildFolderPages(m_entries);
            ShowPage(m_folderPage);

            // Focus the search field once the window is actually up.
            rootVisualElement.schedule.Execute(() => m_searchField.Focus());
        }

        #region Keyboard and Mouse
        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.DownArrow:
                    MoveHighlight(1);
                    break;
                case KeyCode.UpArrow:
                    MoveHighlight(-1);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    PickHighlighted();
                    break;
                case KeyCode.RightArrow:
                    if (IsSearching)
                    {
                        return;
                    }

                    OpenHighlightedFolder();
                    break;
                case KeyCode.LeftArrow:
                case KeyCode.Backspace:
                    if (IsSearching)
                    {
                        return;
                    }

                    GoBack();
                    break;
                case KeyCode.Escape:
                    if (IsSearching)
                    {
                        // Setting the value in code leaves the caret past the end of the empty text.
                        m_searchField.value = string.Empty;
                        m_searchField.SelectRange(0, 0);
                    }
                    else
                    {
                        Close();
                    }

                    break;
                default:
                    return;
            }

            evt.StopPropagation();
        }

        private void OnHeaderPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0)
            {
                GoBack();
            }
        }

        private void OnRowPointerDown(PointerDownEvent evt, int index)
        {
            if (evt.button == 0)
            {
                Highlight(index);
                PickHighlighted();
            }
        }
        #endregion

        #region Pages
        private static Page BuildFolderPages(List<NodeUtils.MenuEntry> entries)
        {
            Page root = new(ROOT_TITLE, null);
            foreach (NodeUtils.MenuEntry entry in entries)
            {
                string[] segments = entry.MenuPath.Split('/');
                Page page = root;
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    page = GetOrAddFolder(page, segments[i]);
                }

                page.Entries.Add(entry);
            }

            return root;
        }

        private static Page GetOrAddFolder(Page parent, string title)
        {
            foreach (Page folder in parent.Folders)
            {
                if (folder.Title == title)
                {
                    return folder;
                }
            }

            Page newFolder = new(title, parent);
            parent.Folders.Add(newFolder);
            return newFolder;
        }

        private void ShowPage(Page page)
        {
            m_page = page;
            m_titleLabel.text = page.Title;
            m_backArrow.style.display = page.Parent != null ? DisplayStyle.Flex : DisplayStyle.None;

            m_rows.Clear();
            foreach (Page folder in page.Folders)
            {
                AddRow(folder.Title, null, true);
            }

            foreach (NodeUtils.MenuEntry entry in page.Entries)
            {
                AddRow(GetEntryName(entry), IsSearching ? GetEntryFolderPath(entry) : null, false);
            }

            Highlight(Mathf.Min(page.HighlightedIndex, m_rows.childCount - 1));
            ScrollToHighlighted();
        }

        private void AddRow(string name, string folderPath, bool isFolder)
        {
            int index = m_rows.childCount;
            VisualElement row = new();
            row.AddToClassList(ROW_CLASS);
            row.RegisterCallback<PointerMoveEvent>(_ => Highlight(index));
            row.RegisterCallback<PointerDownEvent>(evt => OnRowPointerDown(evt, index));

            Label nameLabel = new(name);
            nameLabel.AddToClassList(ROW_NAME_CLASS);
            row.Add(nameLabel);

            if (!string.IsNullOrEmpty(folderPath))
            {
                Label pathLabel = new(folderPath);
                pathLabel.AddToClassList(ROW_PATH_CLASS);
                row.Add(pathLabel);
            }

            if (isFolder)
            {
                Label arrow = new("›");
                arrow.AddToClassList(ROW_ARROW_CLASS);
                row.Add(arrow);
            }

            m_rows.Add(row);
        }

        private void OpenFolder(Page folder)
        {
            m_folderPage = folder;
            ShowPage(folder);
        }

        private void GoBack()
        {
            if (m_page.Parent != null)
            {
                OpenFolder(m_page.Parent);
            }
        }
        #endregion

        #region Highlight
        private void Highlight(int index)
        {
            m_page.HighlightedIndex = index;
            for (int i = 0; i < m_rows.childCount; i++)
            {
                m_rows[i].EnableInClassList(HIGHLIGHTED_ROW_CLASS, i == index);
            }
        }

        private void MoveHighlight(int delta)
        {
            if (m_rows.childCount == 0)
            {
                return;
            }

            Highlight(Mathf.Clamp(m_page.HighlightedIndex + delta, 0, m_rows.childCount - 1));
            ScrollToHighlighted();
        }

        private void ScrollToHighlighted()
        {
            if (m_page.HighlightedIndex >= 0)
            {
                m_rows.ScrollTo(m_rows[m_page.HighlightedIndex]);
            }
        }

        /// <summary>
        /// Opens the highlighted folder, or creates the highlighted node and closes the window.
        /// </summary>
        private void PickHighlighted()
        {
            int index = m_page.HighlightedIndex;
            if (index < 0)
            {
                return;
            }

            if (index < m_page.Folders.Count)
            {
                OpenFolder(m_page.Folders[index]);
                return;
            }

            Type nodeType = m_page.Entries[index - m_page.Folders.Count].NodeType;
            Close();
            m_onSelect(nodeType);
        }

        private void OpenHighlightedFolder()
        {
            int index = m_page.HighlightedIndex;
            if (index >= 0 && index < m_page.Folders.Count)
            {
                OpenFolder(m_page.Folders[index]);
            }
        }
        #endregion

        #region Search
        private void ShowSearchResults(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                ShowPage(m_folderPage);
                return;
            }

            string[] words = searchText.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Page results = new(SEARCH_TITLE, null);
            List<NodeUtils.MenuEntry> laterMatches = new();
            foreach (NodeUtils.MenuEntry entry in m_entries)
            {
                if (!ContainsAllWords(ToSearchKey(entry.MenuPath), words))
                {
                    continue;
                }

                if (words.Length > 0 && ToSearchKey(GetEntryName(entry)).StartsWith(words[0], StringComparison.Ordinal))
                {
                    results.Entries.Add(entry);
                }
                else
                {
                    laterMatches.Add(entry);
                }
            }

            results.Entries.AddRange(laterMatches);
            ShowPage(results);
        }

        private static bool ContainsAllWords(string text, string[] words)
        {
            foreach (string word in words)
            {
                if (!text.Contains(word, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ToSearchKey(string text)
        {
            return text.ToLowerInvariant().Replace(" ", string.Empty);
        }

        private static string GetEntryName(NodeUtils.MenuEntry entry)
        {
            return entry.MenuPath.Substring(entry.MenuPath.LastIndexOf('/') + 1);
        }

        private static string GetEntryFolderPath(NodeUtils.MenuEntry entry)
        {
            int lastSlash = entry.MenuPath.LastIndexOf('/');
            return lastSlash < 0 ? string.Empty : entry.MenuPath.Substring(0, lastSlash);
        }
        #endregion
    }
}
