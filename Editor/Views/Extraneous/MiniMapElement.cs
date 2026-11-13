using Soup.Editor.Views.Edges;
using Soup.Editor.Views.Groups;
using Soup.Editor.Views.Manipulators;
using Soup.Editor.Views.Nodes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    /// <summary>
    /// A miniature of the current node collection, pinned to a corner of the GraphView.
    /// Draws straight from the views rather than the model: node sizes and mid-drag positions only exist there.
    /// </summary>
    public class MiniMapElement : VisualElement
    {
        private const string STYLECLASS_NAME = "minimap";
        private const float CONTENT_PADDING = 6f;
        private const float MIN_ELEMENT_SIZE = 2f;

        private static readonly CustomStyleProperty<Color> NODE_COLOR_PROPERTY = new("--minimap-node-color");
        private static readonly CustomStyleProperty<Color> EDGE_COLOR_PROPERTY = new("--minimap-edge-color");
        private static readonly CustomStyleProperty<Color> SELECTED_COLOR_PROPERTY = new("--minimap-selected-color");
        private static readonly CustomStyleProperty<Color> VIEWPORT_COLOR_PROPERTY = new("--minimap-viewport-color");

        private readonly GraphView m_graphView;
        private readonly VisualElement m_mapElement;
        private readonly UQueryState<BaseNodeView> m_nodes;
        private readonly UQueryState<EdgeView> m_edges;
        private readonly UQueryState<GroupView> m_groups;

        private Color m_defaultNodeColor = new(0.9f, 0.9f, 0.9f, 0.5f);
        private Color m_edgeColor = new(0.9f, 0.9f, 0.9f, 0.3f);
        private Color m_selectedColor = new(0.27f, 0.75f, 1f, 1f);
        private Color m_viewportColor = new(0.9f, 0.9f, 0.9f, 0.8f);

        private float m_scale;
        private Vector2 m_offset;
        private bool m_panning;

        public MiniMapElement(GraphView graphView)
        {
            m_graphView = graphView;
            AddToClassList(STYLECLASS_NAME);

            // Drawn by a child so the frame's overflow clips it.
            m_mapElement = new VisualElement { pickingMode = PickingMode.Ignore };
            m_mapElement.StretchToParentSize();
            m_mapElement.generateVisualContent = OnGenerateVisualContent;
            Add(m_mapElement);

            m_nodes = graphView.ContentContainer.NodesAll;
            m_edges = graphView.ContentContainer.EdgesAll;
            m_groups = graphView.ContentContainer.GroupsAll;

            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);

            // Input
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => EndPan());
            RegisterCallback<WheelEvent>(OnWheel);
            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

            // Nothing may reach the GraphView behind us: its drag manipulator would start a marquee (and clear the selection),
            // and its canvas menu would create nodes under the minimap.
            RegisterCallback<MouseDownEvent>(StopPropagation);
            RegisterCallback<MouseMoveEvent>(StopPropagation);
            RegisterCallback<MouseUpEvent>(StopPropagation);

            // Repaint triggers
            graphView.OnViewTransformChanged += _ => RequestRepaint();
            graphView.OnContentChanged += RequestRepaint;
            graphView.RegisterCallback<GeometryChangedEvent>(_ => RequestRepaint());
        }

        private static void StopPropagation(EventBase evt) => evt.StopPropagation();

        private void RequestRepaint() => m_mapElement.MarkDirtyRepaint();

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            ICustomStyle customStyle = evt.customStyle;

            if (customStyle.TryGetValue(NODE_COLOR_PROPERTY, out Color nodeColor))
            {
                m_defaultNodeColor = nodeColor;
            }

            if (customStyle.TryGetValue(EDGE_COLOR_PROPERTY, out Color edgeColor))
            {
                m_edgeColor = edgeColor;
            }

            if (customStyle.TryGetValue(SELECTED_COLOR_PROPERTY, out Color selectedColor))
            {
                m_selectedColor = selectedColor;
            }

            if (customStyle.TryGetValue(VIEWPORT_COLOR_PROPERTY, out Color viewportColor))
            {
                m_viewportColor = viewportColor;
            }

            RequestRepaint();
        }

        #region Mapping
        private bool UpdateMapping()
        {
            Vector2 size = m_mapElement.layout.size;
            if (!TryGetViewportRect(out Rect bounds) || float.IsNaN(size.x) || float.IsNaN(size.y))
            {
                return false;
            }

            foreach (BaseNodeView node in m_nodes)
            {
                if (TryGetElementRect(node, out Rect nodeRect))
                {
                    bounds = RectUtils.Encompass(bounds, nodeRect);
                }
            }

            foreach (GroupView group in m_groups)
            {
                if (TryGetElementRect(group, out Rect groupRect))
                {
                    bounds = RectUtils.Encompass(bounds, groupRect);
                }
            }

            Vector2 available = size - Vector2.one * (CONTENT_PADDING * 2f);
            if (available.x <= 0f || available.y <= 0f || bounds.width <= 0f || bounds.height <= 0f)
            {
                return false;
            }

            m_scale = Mathf.Min(available.x / bounds.width, available.y / bounds.height);
            m_offset = size * 0.5f - bounds.center * m_scale;
            return true;
        }

        private bool TryGetViewportRect(out Rect viewport)
        {
            Vector2 size = m_graphView.layout.size;
            float scale = m_graphView.CurrentScale;
            if (float.IsNaN(size.x) || float.IsNaN(size.y) || size.x <= 0f || size.y <= 0f || scale <= 0f)
            {
                viewport = default;
                return false;
            }

            viewport = new Rect(-(Vector2)m_graphView.GetViewTransformPosition() / scale, size / scale);
            return true;
        }

        private bool TryGetElementRect(GraphElement element, out Rect rect)
        {
            rect = element.ChangeCoordinatesTo(m_graphView.ContentContainer, element.Rect());
            return !float.IsNaN(rect.x) && !float.IsNaN(rect.y) && !float.IsNaN(rect.width) && !float.IsNaN(rect.height);
        }

        private Vector2 ToMiniMap(Vector2 contentPosition) => contentPosition * m_scale + m_offset;

        private Vector2 ToContent(Vector2 miniMapPosition) => (miniMapPosition - m_offset) / m_scale;

        private Rect ToMiniMap(Rect contentRect, float minSize = 0f)
        {
            Vector2 size = contentRect.size * m_scale;
            size.x = Mathf.Max(size.x, minSize);
            size.y = Mathf.Max(size.y, minSize);
            return new Rect(ToMiniMap(contentRect.position), size);
        }
        #endregion

        #region Drawing
        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (!m_panning && !UpdateMapping())
            {
                return;
            }

            if (!TryGetViewportRect(out Rect viewport))
            {
                return;
            }

            Painter2D painter = ctx.painter2D;
            painter.lineWidth = 1f;

            foreach (GroupView group in m_groups)
            {
                if (!TryGetElementRect(group, out Rect groupRect))
                {
                    continue;
                }

                PathRectangle(painter, ToMiniMap(groupRect, MIN_ELEMENT_SIZE));
                Color fill = group.resolvedStyle.backgroundColor; // Resolved by the time layout has run, and groups without layout were skipped above.
                fill.a = 1f; // Opaque, or the dark group colours disappear into the minimap's own background.
                painter.fillColor = fill;
                painter.Fill();

                if (group.Selected)
                {
                    painter.strokeColor = m_selectedColor;
                    painter.Stroke();
                }
            }

            foreach (EdgeView edge in m_edges)
            {
                if (!edge.IsRealEdge() && !edge.IsCandidateEdge())
                {
                    continue;
                }

                painter.strokeColor = edge.Selected ? m_selectedColor : m_edgeColor;
                painter.BeginPath();
                painter.MoveTo(ToMiniMap(edge.From));
                painter.LineTo(ToMiniMap(edge.To));
                painter.Stroke();
            }

            foreach (BaseNodeView node in m_nodes)
            {
                if (!TryGetElementRect(node, out Rect nodeRect))
                {
                    continue;
                }

                PathRectangle(painter, ToMiniMap(nodeRect, MIN_ELEMENT_SIZE));
                
                painter.fillColor = node.MetaData.Color;
                painter.Fill();

                if (node.Selected)
                {
                    painter.strokeColor = m_selectedColor;
                    painter.Stroke();
                }
            }

            PathRectangle(painter, ToMiniMap(viewport));
            painter.strokeColor = m_viewportColor;
            painter.Stroke();
        }

        private static void PathRectangle(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
        }
        #endregion

        #region Input
        private void OnPointerDown(PointerDownEvent evt)
        {
            evt.StopPropagation();

            if (evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            m_graphView.Focus(); // Keyboard shortcuts live on the graph.

            if (!UpdateMapping())
            {
                return;
            }

            m_panning = true;
            this.CapturePointer(evt.pointerId);
            CenterViewportOn(m_mapElement.WorldToLocal(evt.position));
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            evt.StopPropagation();

            if (m_panning && this.HasPointerCapture(evt.pointerId))
            {
                CenterViewportOn(m_mapElement.WorldToLocal(evt.position));
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            evt.StopPropagation();

            if (m_panning && this.HasPointerCapture(evt.pointerId))
            {
                this.ReleasePointer(evt.pointerId); // Ends the pan through PointerCaptureOutEvent.
            }
        }

        private void EndPan()
        {
            if (!m_panning)
            {
                return;
            }

            m_panning = false;
            RequestRepaint(); // Refit now that the mapping is unfrozen.
        }

        private void CenterViewportOn(Vector2 miniMapPosition)
        {
            CenterView(ToContent(miniMapPosition), m_graphView.CurrentScale);
        }

        private void CenterView(Vector2 contentPosition, float scale)
        {
            Vector2 translation = m_graphView.layout.size * 0.5f - contentPosition * scale;
            m_graphView.UpdateViewTransform(translation, new(scale, scale, 1f));
        }

        private void OnWheel(WheelEvent evt)
        {
            evt.StopPropagation();

            if (panel?.GetCapturingElement(PointerId.mousePointerId) != null || !TryGetViewportRect(out Rect viewport))
            {
                return;
            }

            float zoom = ZoomManipulator.CalculateNewZoom(m_graphView.CurrentScale, -evt.delta.y, m_graphView.ScaleStep, m_graphView.ReferenceScale, m_graphView.MinScale, m_graphView.MaxScale);
            CenterView(viewport.center, zoom);
        }

        private void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction(GraphShortcuts.WithHint("Hide MiniMap", GraphShortcuts.TOGGLE_MINIMAP), _ => m_graphView.MiniMapVisible = false);
            evt.StopPropagation(); // Keep the canvas menu out of it.
        }
        #endregion
    }
}
