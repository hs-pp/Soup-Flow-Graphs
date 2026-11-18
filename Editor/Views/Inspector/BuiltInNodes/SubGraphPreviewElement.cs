using System;
using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector
{
    /// <summary>
    /// Thumbnail of a sub graph's NodeCollection.
    /// </summary>
    [UxmlElement]
    public partial class SubGraphPreviewElement : VisualElement
    {
        private static readonly Vector2 NODE_SIZE = new(160f, 60f);
        private const float ASPECT_RATIO = 9f / 16f;
        private const float PADDING = 10f;
        private const float MAX_SCALE = 0.5f; // Keeps a lone entry node from filling the whole preview.
        private const float MIN_NODE_SIZE = 3f;
        private static readonly Color EDGE_COLOR = new(1f, 1f, 1f, 0.45f);
        private const string OPEN_LABEL_CLASS = "subGraphPreviewOpenLabel";

        private readonly struct NodeShape
        {
            public readonly Rect Rect;
            public readonly Color Color;

            public NodeShape(Rect rect, Color color)
            {
                Rect = rect;
                Color = color;
            }
        }

        private readonly List<NodeShape> m_nodes = new();
        private readonly List<NodeShape> m_groups = new();
        private readonly List<(Vector2 from, Vector2 to)> m_edges = new();
        private Rect m_bounds;

        public event Action OnClicked;

        public SubGraphPreviewElement()
        {
            tooltip = "Open sub graph";

            Label openLabel = new("Open ↳") { pickingMode = PickingMode.Ignore };
            openLabel.AddToClassList(OPEN_LABEL_CLASS);
            Add(openLabel);

            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<ClickEvent>(_ => OnClicked?.Invoke());
        }

        public void SetContents(NodeCollectionRef collection, NodeCollectionMetaDataRef metaData)
        {
            m_nodes.Clear();
            m_groups.Clear();
            m_edges.Clear();

            Dictionary<string, Rect> rectsById = new();
            if (collection.IsValid && metaData.IsValid)
            {
                Dictionary<string, NodeMetaDataRef> metaDataById = metaData.GetNodesById();
                foreach (NodeRef node in collection.GetNodes())
                {
                    if (node.IsMissingType || !metaDataById.TryGetValue(node.Id, out NodeMetaDataRef nodeMetaData))
                    {
                        continue;
                    }

                    Rect rect = new(nodeMetaData.Position, NODE_SIZE);
                    rectsById[node.Id] = rect;
                    Color color = nodeMetaData.Color;
                    color.a = 1f;
                    m_nodes.Add(new NodeShape(rect, color));
                }

                foreach (NodeRef node in collection.GetNodes())
                {
                    if (node.IsMissingType || !rectsById.TryGetValue(node.Id, out Rect fromRect))
                    {
                        continue;
                    }

                    foreach (OutportRef outport in node.EnumerateOutports())
                    {
                        string targetId = outport.ConnectedNodeId;
                        if (!string.IsNullOrEmpty(targetId) && rectsById.TryGetValue(targetId, out Rect toRect))
                        {
                            m_edges.Add((new Vector2(fromRect.xMax, fromRect.center.y), new Vector2(toRect.xMin, toRect.center.y)));
                        }
                    }
                }

                foreach (GroupMetaDataRef group in metaData.GetGroups())
                {
                    m_groups.Add(new NodeShape(group.Rect, group.Color));
                }
            }

            m_bounds = ComputeBounds();
            MarkDirtyRepaint();
        }

        private Rect ComputeBounds()
        {
            bool hasAny = false;
            Rect bounds = default;
            foreach (NodeShape shape in m_nodes)
            {
                bounds = hasAny ? Encapsulate(bounds, shape.Rect) : shape.Rect;
                hasAny = true;
            }
            foreach (NodeShape shape in m_groups)
            {
                bounds = hasAny ? Encapsulate(bounds, shape.Rect) : shape.Rect;
                hasAny = true;
            }

            return bounds;
        }

        private static Rect Encapsulate(Rect a, Rect b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            float height = Mathf.Round(evt.newRect.width * ASPECT_RATIO);
            if (!Mathf.Approximately(resolvedStyle.height, height))
            {
                style.height = height;
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (m_nodes.Count == 0 && m_groups.Count == 0)
            {
                return;
            }

            Rect area = contentRect;
            area.xMin += PADDING;
            area.yMin += PADDING;
            area.xMax -= PADDING;
            area.yMax -= PADDING;
            if (area.width <= 0f || area.height <= 0f || m_bounds.width <= 0f || m_bounds.height <= 0f)
            {
                return;
            }

            float scale = Mathf.Min(MAX_SCALE, Mathf.Min(area.width / m_bounds.width, area.height / m_bounds.height));
            Vector2 offset = area.center - m_bounds.center * scale;

            Painter2D painter = ctx.painter2D;

            painter.lineWidth = 1f;
            foreach (NodeShape group in m_groups)
            {
                Rect rect = ToPreview(group.Rect, scale, offset, 0f);
                Color fill = group.Color;
                fill.a *= 0.5f;
                painter.fillColor = fill;
                painter.strokeColor = group.Color;
                RectPath(painter, rect);
                painter.Fill();
                painter.Stroke();
            }

            painter.strokeColor = EDGE_COLOR;
            foreach ((Vector2 from, Vector2 to) in m_edges)
            {
                painter.BeginPath();
                painter.MoveTo(from * scale + offset);
                painter.LineTo(to * scale + offset);
                painter.Stroke();
            }

            foreach (NodeShape node in m_nodes)
            {
                Rect rect = ToPreview(node.Rect, scale, offset, MIN_NODE_SIZE);
                painter.fillColor = node.Color;
                RectPath(painter, rect);
                painter.Fill();
            }
        }

        private static Rect ToPreview(Rect graphRect, float scale, Vector2 offset, float minSize)
        {
            Vector2 size = Vector2.Max(graphRect.size * scale, new Vector2(minSize, minSize));
            Vector2 center = graphRect.center * scale + offset;
            return new Rect(center - size * 0.5f, size);
        }

        private static void RectPath(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            painter.MoveTo(new(rect.xMin, rect.yMin));
            painter.LineTo(new(rect.xMax, rect.yMin));
            painter.LineTo(new(rect.xMax, rect.yMax));
            painter.LineTo(new(rect.xMin, rect.yMax));
            painter.ClosePath();
        }
    }
}
