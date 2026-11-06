using System;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Soup.Editor.Views
{
    /// <summary>
    /// Active while a <see cref="GraphView"/> itself holds keyboard focus. Shortcuts in this context beat Unity's global ones
    /// (Cmd/Ctrl+G is Assets > Generate), and plain keys still reach a text field when one has focus instead of the graph.
    /// </summary>
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

        // Called by every GraphView rather than on load, so it can't run before the Shortcut Manager is up. Statics reset on
        // domain reload, which re-registers.
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
    /// Soup-only graph hotkeys, rebindable under Edit > Shortcuts > Soup. Actions Unity's Edit menu already has (copy, cut,
    /// paste, duplicate, select all, delete, frame) reach the GraphView as command events instead.
    /// </summary>
    internal static class GraphShortcuts
    {
        public const string CREATE_NODE = "Soup/Create Node";
        public const string TOGGLE_MINIMAP = "Soup/Toggle MiniMap";
        public const string GROUP_SELECTION = "Soup/Group Selection";
        public const string DELETE_SELECTION = "Soup/Delete Selection";
        public const string DELETE_SELECTION_BACKSPACE = "Soup/Delete Selection (Backspace)";

        // Unity's bindings for the Edit-menu actions, so hints follow the user's rebinding.
        public const string EDIT_PASTE = "Main Menu/Edit/Paste";
        public const string EDIT_DUPLICATE = "Main Menu/Edit/Duplicate";
        public const string EDIT_FRAME = "Main Menu/Edit/Frame Selected in Window under Cursor";

        // Mac keyboards label Backspace "delete", so that's the key to show there.
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

        // The key is taken even mid-drag, so it can't fall through to a global shortcut.
        private static void Run(Action<GraphView> action)
        {
            GraphView graph = GraphShortcutContext.FocusedGraph;
            if (graph != null && !graph.IsMouseCaptured)
            {
                action(graph);
            }
        }

        /// <summary>
        /// "Group Selection (⌘G)" for a bound shortcut, the bare label otherwise.
        /// </summary>
        public static string WithHint(string label, string shortcutId)
        {
            string binding;
            try
            {
                binding = ShortcutManager.instance.GetShortcutBinding(shortcutId).ToString();
            }
            catch (ArgumentException) // Unknown id, e.g. a Unity version that renamed its menu item.
            {
                return label;
            }

            return string.IsNullOrEmpty(binding) ? label : $"{label} ({binding})";
        }
    }
}
