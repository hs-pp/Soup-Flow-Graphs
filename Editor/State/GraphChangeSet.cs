using System.Collections.Generic;

namespace Soup.Editor.State
{
    public sealed class GraphChangeSet
    {
        public readonly HashSet<string> AddedNodes = new();
        public readonly HashSet<string> RemovedNodes = new();
        public readonly HashSet<string> MovedNodes = new();
        public readonly HashSet<string> ChangedNodes = new(); // Data edits. One that changes outports also raises TopologyDirty.
        public readonly HashSet<string> AddedGroups = new();

        public bool TopologyDirty { get; set; } // Edges were changed
        public bool GroupsDirty { get; set; } // Groups were edited
        public bool BlackboardDirty { get; set; } // Blackboard variables were added, removed, renamed or reordered

        public bool HasChanges => IsStructural
                                  || MovedNodes.Count > 0
                                  || ChangedNodes.Count > 0
                                  || GroupsDirty
                                  || BlackboardDirty;

        public bool IsStructural => AddedNodes.Count > 0
                                    || RemovedNodes.Count > 0
                                    || TopologyDirty;

        public GraphChangeSet Copy()
        {
            GraphChangeSet copy = new();
            copy.AddedNodes.UnionWith(AddedNodes);
            copy.RemovedNodes.UnionWith(RemovedNodes);
            copy.MovedNodes.UnionWith(MovedNodes);
            copy.ChangedNodes.UnionWith(ChangedNodes);
            copy.AddedGroups.UnionWith(AddedGroups);
            copy.TopologyDirty = TopologyDirty;
            copy.GroupsDirty = GroupsDirty;
            copy.BlackboardDirty = BlackboardDirty;
            return copy;
        }

        public void Clear()
        {
            AddedNodes.Clear();
            RemovedNodes.Clear();
            MovedNodes.Clear();
            ChangedNodes.Clear();
            AddedGroups.Clear();
            TopologyDirty = false;
            GroupsDirty = false;
            BlackboardDirty = false;
        }
    }
}
