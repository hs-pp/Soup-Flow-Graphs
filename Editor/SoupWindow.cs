using Soup.Editor.State;
using Soup.Editor.Views;
using Soup.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Soup.Editor
{
    public class SoupWindow : EditorWindow, IHasCustomMenu
    {
        [SerializeField]
        private GraphEditorState m_state;
        
        private SoupEditorView m_editorView;
        
        [MenuItem("Window/Soup")]
        public static void OpenWindow()
        {
            GetWindow<SoupWindow>("Soup").Show();
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceID, int line)
        {
            if (EditorUtility.EntityIdToObject(instanceID) is not AFlowGraph graph)
            {
                return false;
            }

            string assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph));
            foreach (SoupWindow open in Resources.FindObjectsOfTypeAll<SoupWindow>())
            {
                if (open.m_state != null && open.m_state.AssetGuid == assetGuid)
                {
                    open.Focus();
                    return true;
                }
            }

            SoupWindow window = GetWindow<SoupWindow>("Soup");
            window.LoadGraph(assetGuid);
            window.Show();
            return true;
        }

        private static void OpenInNewWindow(string assetGuid)
        {
            SoupWindow window = CreateInstance<SoupWindow>();
            window.titleContent = new GUIContent("Soup");
            window.LoadGraph(assetGuid);
            window.Show();
        }
        
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Open in New Window"), false, () => OpenInNewWindow(m_state?.AssetGuid));
            menu.AddItem(new GUIContent("Force Recompile"), false, () => { UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); });
            menu.AddItem(new GUIContent("Force Rebuild Editor"), false, CreateGUI);
        }

        private void OnDisable()
        {
            m_editorView?.Dispose();
        }

        private void CreateGUI()
        {
            if (m_state == null)
            {
                m_state = new GraphEditorState();
            }
            
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
