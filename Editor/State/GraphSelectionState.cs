using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soup.Editor.State
{
    public enum GraphSelectionMode
    {
        Replace,
        Add,
        Remove,
        Toggle,
    }
    
    [Serializable]
    public sealed class GraphSelectionState
    {
        [SerializeField]
        private List<string> m_selectedIds = new();
        public IReadOnlyList<string> SelectedIds => m_selectedIds;

        public bool Apply(GraphSelectionMode mode, IReadOnlyList<string> ids)
        {
            bool changed = false;
            switch (mode)
            {
                case GraphSelectionMode.Replace:
                    m_selectedIds.Clear();
                    m_selectedIds.AddRange(ids);
                    changed = true; // Just always return true here.
                    break;

                case GraphSelectionMode.Add:
                    foreach (string id in ids)
                    {
                        if (!m_selectedIds.Contains(id))
                        {
                            m_selectedIds.Add(id);
                            changed = true;
                        }
                    }
                    break;

                case GraphSelectionMode.Remove:
                    foreach (string id in ids)
                    {
                        changed |= m_selectedIds.Remove(id);
                    }
                    break;

                case GraphSelectionMode.Toggle:
                    foreach (string id in ids)
                    {
                        if (!m_selectedIds.Remove(id))
                        {
                            m_selectedIds.Add(id);
                        }

                        changed = true;
                    }
                    break;
            }

            return changed;
        }

        public bool RemoveInvalid(HashSet<string> validIds)
        {
            return m_selectedIds.RemoveAll(id => !validIds.Contains(id)) > 0;
        }

        public void Clear()
        {
            m_selectedIds.Clear();
        }
    }
}
