using Soup.Editor.State;
using Soup.Editor.Views;
using Soup.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Soup.Editor
{
    /// <summary>
    /// The Soup editor window. It shows one graph through a SoupEditorView, and serializes its GraphEditorState so the
    /// open graph and view survive domain reloads. Several windows can show the same graph.
    /// </summary>
    public class SoupWindow : EditorWindow, IHasCustomMenu
    {
        private const string WINDOW_TITLE = "Soup";
        [SerializeField]
        private GraphEditorState m_state = new();
        
        private SoupEditorView m_editorView;
        
        [MenuItem("Window/Soup")]
        private static void OpenWindow()
        {
            GetWindow<SoupWindow>(WINDOW_TITLE);
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line)
        {
            if (EditorUtility.EntityIdToObject(entityId) is not AFlowGraph graph)
            {
                return false;
            }

            string assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph));
            foreach (SoupWindow existing in Resources.FindObjectsOfTypeAll<SoupWindow>())
            {
                if (existing.m_state.AssetGuid == assetGuid)
                {
                    existing.Focus();
                    return true;
                }
            }

            SoupWindow window = GetWindow<SoupWindow>(WINDOW_TITLE);
            window.LoadGraph(assetGuid);
            return true;
        }

        private static void OpenInNewWindow(string assetGuid)
        {
            SoupWindow window = CreateInstance<SoupWindow>();
            window.titleContent = new GUIContent(WINDOW_TITLE);
            window.LoadGraph(assetGuid);
            window.Show();
        }
        
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Open in New Window"), false, () => OpenInNewWindow(m_state.AssetGuid));
            menu.AddItem(new GUIContent("Force Rebuild Editor"), false, CreateGUI);
        }

        private void OnDisable()
        {
            m_editorView?.Dispose();
        }

        private void CreateGUI()
        {
            if (m_editorView != null)
            {
                m_editorView.Dispose();
                rootVisualElement.Remove(m_editorView);
            }
            m_editorView = new SoupEditorView(m_state);
            m_editorView.style.flexGrow = 1;
            rootVisualElement.Add(m_editorView);
        }

        private void LoadGraph(string assetGuid)
        {
            if (m_editorView == null)
            {
                m_state = new GraphEditorState(assetGuid);
                return;
            }

            m_editorView.LoadGraph(assetGuid);
        }
    }
}
