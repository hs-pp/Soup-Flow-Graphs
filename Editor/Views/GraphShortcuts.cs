using System;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Soup.Editor.Views
{
    internal sealed class GraphShortcutContext : IShortcutContext
    {
        private static GraphShortcutContext s_instance;

        public bool active => FocusedGraph != null;

        public static GraphView FocusedGraph
        {
            get
            {
                EditorWindow window = EditorWindow.focusedWindow;
                return window != null ? window.rootVisualElement.focusController?.focusedElement as GraphView : null;
            }
        }

        public static void EnsureRegistered()
        {
            if (s_instance != null)
            {
                return;
            }

            s_instance = new GraphShortcutContext();
            ShortcutManager.RegisterContext(s_instance);
        }
    }

    /// <summary>
    /// Rebindable under Edit > Shortcuts > Soup.
    /// </summary>
    internal static class GraphShortcuts
    {
        public const string CREATE_NODE = "Soup/Create Node";
        public const string TOGGLE_MINIMAP = "Soup/Toggle MiniMap";
        public const string GROUP_SELECTION = "Soup/Group Selection";
        public const string DELETE_SELECTION = "Soup/Delete Selection";
        public const string DELETE_SELECTION_BACKSPACE = "Soup/Delete Selection (Backspace)";
        public const string EDIT_PASTE = "Main Menu/Edit/Paste";
        public const string EDIT_DUPLICATE = "Main Menu/Edit/Duplicate";
        public const string EDIT_FRAME = "Main Menu/Edit/Frame Selected in Window under Cursor";

        public static string DeleteSelectionHintId => Application.platform == RuntimePlatform.OSXEditor ? DELETE_SELECTION_BACKSPACE : DELETE_SELECTION;

        [Shortcut(CREATE_NODE, typeof(GraphShortcutContext), KeyCode.Space)]
        private static void CreateNode() => Run(graph => graph.OpenNodeSearchAtMouse());

        [Shortcut(TOGGLE_MINIMAP, typeof(GraphShortcutContext), KeyCode.M)]
        private static void ToggleMiniMap() => Run(graph => graph.MiniMapVisible = !graph.MiniMapVisible);

        [Shortcut(GROUP_SELECTION, typeof(GraphShortcutContext), KeyCode.G, ShortcutModifiers.Action)]
        private static void GroupSelection() => Run(graph => graph.GroupSelection());

        [Shortcut(DELETE_SELECTION, typeof(GraphShortcutContext), KeyCode.Delete)]
        private static void DeleteSelection() => Run(graph => graph.RequestDeleteSelection());

        [Shortcut(DELETE_SELECTION_BACKSPACE, typeof(GraphShortcutContext), KeyCode.Backspace)]
        private static void DeleteSelectionBackspace() => Run(graph => graph.RequestDeleteSelection());

        private static void Run(Action<GraphView> action)
        {
            GraphView graph = GraphShortcutContext.FocusedGraph;
            if (graph != null && !graph.IsMouseCaptured)
            {
                action(graph);
            }
        }

        public static string WithHint(string label, string shortcutId)
        {
            string binding;
            try
            {
                binding = ShortcutManager.instance.GetShortcutBinding(shortcutId).ToString();
            }
            catch (ArgumentException)
            {
                return label;
            }

            return string.IsNullOrEmpty(binding) ? label : $"{label} ({binding})";
        }
    }
}
