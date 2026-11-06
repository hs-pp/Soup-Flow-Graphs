using System.Collections.Generic;
using Soup.Editor.Views;
using UnityEditor;

namespace Soup.Editor.Core
{
    public class LoadedFlowGraphHandler
    {
        public string AssetGuid { get; private set; }
        public LoadedFlowGraph LoadedGraph { get; private set; }
        private List<SoupEditorView> m_requesters = new();
        public IReadOnlyList<SoupEditorView> Requesters => m_requesters;

        public LoadedFlowGraphHandler(string assetGuid, LoadedFlowGraph loadedGraph)
        {
            AssetGuid = assetGuid;
            LoadedGraph = loadedGraph;
        }

        public void AddRequester(SoupEditorView requestedEditor)
        {
            if (!m_requesters.Contains(requestedEditor))
            {
                m_requesters.Add(requestedEditor);
            }
        }

        public void RemoveRequester(SoupEditorView requestedEditor)
        {
            m_requesters.Remove(requestedEditor);
        }

        public bool HasAnyRequesters()
        {
            return  m_requesters.Count != 0;
        }

        public void ClearHandler()
        {
            LoadedGraph.Dispose();
        }
    }
    
    public static class SoupEditorCore
    {
        private static Dictionary<string, LoadedFlowGraphHandler> m_loadedGraphs = new();
        
        public static LoadedFlowGraph RequestLoadedGraph(string assetGuid, SoupEditorView requestedEditor)
        {
            if (string.IsNullOrEmpty(assetGuid))
            {
                return null;
            }

            if (!m_loadedGraphs.TryGetValue(assetGuid, out LoadedFlowGraphHandler handler))
            {
                if (!LoadedFlowGraph.TryLoad(assetGuid, out LoadedFlowGraph loaded))
                {
                    return null;
                }

                handler = new LoadedFlowGraphHandler(assetGuid, loaded);
                m_loadedGraphs.Add(assetGuid, handler);
            }
            handler.AddRequester(requestedEditor);
            
            return handler.LoadedGraph;
        }

        public static void ReleaseLoadedGraph(string assetGuid, SoupEditorView requestedEditor)
        {
            if (string.IsNullOrEmpty(assetGuid))
            {
                return;
            }

            if (!m_loadedGraphs.ContainsKey(assetGuid))
            {
                return;
            }
            
            LoadedFlowGraphHandler handler = m_loadedGraphs[assetGuid];
            handler.RemoveRequester(requestedEditor);
            if (!handler.HasAnyRequesters())
            {
                handler.ClearHandler();
                m_loadedGraphs.Remove(assetGuid);
            }
        }

        internal static void UnloadDeletedGraphs()
        {
            List<SoupEditorView> orphaned = new();
            foreach (LoadedFlowGraphHandler handler in m_loadedGraphs.Values)
            {
                if (!handler.LoadedGraph.IsGraphAlive)
                {
                    orphaned.AddRange(handler.Requesters);
                }
            }

            // Loop through these separately to avoid array changing mid loop.
            foreach (SoupEditorView view in orphaned)
            {
                view.UnloadGraph();
            }
        }

        internal static void ReloadImportedGraphs(string[] assetPaths)
        {
            if (m_loadedGraphs.Count == 0)
            {
                return;
            }

            foreach (string path in assetPaths)
            {
                if (m_loadedGraphs.TryGetValue(AssetDatabase.AssetPathToGUID(path), out LoadedFlowGraphHandler handler))
                {
                    handler.LoadedGraph.HandleReimported();
                }
            }
        }
    }
}
