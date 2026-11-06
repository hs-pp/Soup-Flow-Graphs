using System.Collections.Generic;
using Soup.Editor.Views.Edges;
using Soup.Editor.Views.Groups;
using Soup.Editor.Views.Nodes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views
{
    public class GraphElementContainer : VisualElement
    {
        public GraphElementContainer()
        {
            AddToClassList("contentViewContainer");
            pickingMode = PickingMode.Ignore;
            usageHints = UsageHints.GroupTransform;

            // Cached Queries
            ElementsAll = this.Query<GraphElement>().Build();
            NodesAll = this.Query<BaseNodeView>().Build();
            NodesSelected = this.Query<BaseNodeView>().Where(WhereSelected).Build();
            GroupsAll = this.Query<GroupView>().Build();
            GroupsSelected = this.Query<GroupView>().Where(WhereSelected).Build();
            EdgesAll = this.Query<EdgeView>().Build();
            EdgesSelected = this.Query<EdgeView>().Where(WhereSelected).Build();
            Ports = this.Query<PortView>().Build();
        }

        #region Selection
        public UQueryState<BaseNodeView> NodesAll { get; }
        public UQueryState<BaseNodeView> NodesSelected { get; }
        public UQueryState<GroupView> GroupsAll { get; }
        public UQueryState<GroupView> GroupsSelected { get; }
        public UQueryState<PortView> Ports { get; }
        public UQueryState<EdgeView> EdgesAll { get; }
        public UQueryState<EdgeView> EdgesSelected { get; }
        public UQueryState<GraphElement> ElementsAll { get; }

        private bool WhereSelected(GraphElement element) => element.Selected;

        public List<GraphElement> GetGroupContents(GroupView group)
        {
            HashSet<GraphElement> contents = new();
            CollectGroupContents(group, contents);
            return new List<GraphElement>(contents);
        }

        internal void CollectGroupContents(GroupView group, HashSet<GraphElement> into)
        {
            Rect bounds = group.CanvasRect;

            foreach (GroupView other in GroupsAll)
            {
                if (other != group && FullyContains(bounds, other.CanvasRect))
                {
                    into.Add(other);
                }
            }

            foreach (BaseNodeView node in NodesAll)
            {
                if (node.IsMovable() && FullyContains(bounds, node.CanvasRect))
                {
                    into.Add(node);
                }
            }
        }

        private static bool FullyContains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin && inner.xMax <= outer.xMax && inner.yMin >= outer.yMin && inner.yMax <= outer.yMax;
        }

        #endregion
    }
}
