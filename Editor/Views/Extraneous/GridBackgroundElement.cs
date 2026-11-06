using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    public class GridBackgroundElement : VisualElement
    {
        // 4 vertices per dot keeps one allocation under the 65535-vertex mesh limit.
        private const int MAX_DOTS_PER_MESH = 16000;

        private static readonly CustomStyleProperty<float> s_spacingProperty = new("--grid-spacing");
        private static readonly CustomStyleProperty<float> s_minDotSpacingProperty = new("--grid-min-dot-spacing");
        private static readonly CustomStyleProperty<float> s_dotSizeProperty = new("--grid-dot-size");
        private static readonly CustomStyleProperty<float> s_minDotSizeProperty = new("--grid-min-dot-size");
        private static readonly CustomStyleProperty<Color> s_dotColorProperty = new("--grid-dot-color");

        private VisualElement m_Container;
        private GraphAxesElement m_graphAxesElement;
        private GraphView m_GraphView;

        private float m_Spacing;
        private float m_MinDotSpacing;
        private float m_DotSize;
        private float m_MinDotSize;
        private Color m_DotColor;

        public GridBackgroundElement()
        {
            AddToClassList("gridBackground");
            pickingMode = PickingMode.Ignore;
            this.StretchToParentSize();
            generateVisualContent = OnGenerateVisualContent;
            RegisterCallback<AttachToPanelEvent>(OnAttachEvent);
            RegisterCallback<DetachFromPanelEvent>(DetachFromPanelEvent);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            ICustomStyle customStyle = e.customStyle;

            if (customStyle.TryGetValue(s_spacingProperty, out float spacing))
            {
                m_Spacing = spacing;
            }

            if (customStyle.TryGetValue(s_minDotSpacingProperty, out float minDotSpacing))
            {
                m_MinDotSpacing = minDotSpacing;
            }

            if (customStyle.TryGetValue(s_dotSizeProperty, out float dotSize))
            {
                m_DotSize = dotSize;
            }

            if (customStyle.TryGetValue(s_minDotSizeProperty, out float minDotSize))
            {
                m_MinDotSize = minDotSize;
            }

            if (customStyle.TryGetValue(s_dotColorProperty, out Color dotColor))
            {
                m_DotColor = dotColor;
            }

            MarkDirtyRepaint();
        }

        private void OnAttachEvent(AttachToPanelEvent evt)
        {
            // Parent must be GraphView
            VisualElement target = parent;
            m_GraphView = target as GraphView;
            if (m_GraphView == null)
            {
                throw new InvalidOperationException("GridBackground can only be added to a GraphView");
            }

            m_Container = m_GraphView.ContentContainer;

            m_graphAxesElement = new GraphAxesElement(m_GraphView);
            Insert(0, m_graphAxesElement);

            // Listen for Zoom/Pan Changes
            m_GraphView.OnViewTransformChanged += RequestRepaint;
        }

        private void DetachFromPanelEvent(DetachFromPanelEvent evt)
        {
            // Stop Listening for Zoom/Pan Changes
            m_GraphView.OnViewTransformChanged -= RequestRepaint;

            if (m_graphAxesElement != null)
            {
                Remove(m_graphAxesElement);
            }
        }

        private void RequestRepaint(GraphElementContainer contentContainer) => MarkDirtyRepaint();

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            // Unset until the --grid-* properties resolve.
            if (m_Spacing <= 0f || m_DotSize <= 0f || m_DotColor.a <= 0f)
            {
                return;
            }

            Rect clientRect = m_GraphView.layout;
            Vector3 containerTranslation = m_Container.resolvedStyle.translate;
            Vector2 containerScale = m_Container.resolvedStyle.scale.value;
            Rect containerPosition = m_Container.layout;

            // Screen-space position of the graph origin, so dots stay aligned to it while panning.
            Vector2 origin = new(
                containerTranslation.x + containerPosition.x * containerScale.x,
                containerTranslation.y + containerPosition.y * containerScale.y);

            // Dots shrink with zoom, down to a floor that keeps them visible when fully zoomed out.
            float dotSize = Mathf.Max(m_MinDotSize, m_DotSize * containerScale.x);

            // Spacing shrinks faster than the floored dots, so zooming out would pack them into noise. Doubling
            // the step whenever dots get too close thins them to every 2nd, 4th, ... dot, which stays origin-aligned.
            float minScreenSpacing = Mathf.Max(m_MinDotSpacing, dotSize * 2f);
            float stepX = m_Spacing * containerScale.x;
            float stepY = m_Spacing * containerScale.y;
            if (stepX <= 0f || stepY <= 0f)
            {
                return;
            }

            while (Mathf.Min(stepX, stepY) < minScreenSpacing)
            {
                stepX *= 2f;
                stepY *= 2f;
            }

            float halfSize = dotSize * 0.5f;
            float firstX = FirstDotPosition(origin.x, stepX, halfSize);
            float firstY = FirstDotPosition(origin.y, stepY, halfSize);
            int columns = Mathf.CeilToInt((clientRect.width + halfSize - firstX) / stepX);
            int rows = Mathf.CeilToInt((clientRect.height + halfSize - firstY) / stepY);

            // The axes cover x = 0 and y = 0, so their column and row of dots is skipped. Dots are origin-aligned,
            // so these are whole columns/rows; an origin off screen leaves its index outside the range.
            int originColumn = Mathf.RoundToInt((origin.x - firstX) / stepX);
            int originRow = Mathf.RoundToInt((origin.y - firstY) / stepY);
            bool skipColumn = originColumn >= 0 && originColumn < columns;
            bool skipRow = originRow >= 0 && originRow < rows;
            int drawnColumns = skipColumn ? columns - 1 : columns;
            int drawnRows = skipRow ? rows - 1 : rows;
            if (drawnColumns <= 0 || drawnRows <= 0)
            {
                return;
            }

            // Snap each dot to physical pixels so it keeps one size instead of shimmering while panning.
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float snappedSize = Mathf.Max(1f, Mathf.Floor(dotSize * pixelsPerPoint + 0.5f)) / pixelsPerPoint;
            Color32 tint = m_DotColor;

            int dotCount = drawnColumns * drawnRows;
            int dot = 0;
            while (dot < dotCount)
            {
                int batch = Math.Min(dotCount - dot, MAX_DOTS_PER_MESH);
                MeshWriteData mesh = ctx.Allocate(batch * 4, batch * 6);
                for (int i = 0; i < batch; i++, dot++)
                {
                    int column = dot % drawnColumns;
                    int row = dot / drawnColumns;
                    if (skipColumn && column >= originColumn)
                    {
                        column++;
                    }

                    if (skipRow && row >= originRow)
                    {
                        row++;
                    }

                    float x = firstX + column * stepX - halfSize;
                    float y = firstY + row * stepY - halfSize;
                    float left = Mathf.Floor(x * pixelsPerPoint + 0.5f) / pixelsPerPoint;
                    float top = Mathf.Floor(y * pixelsPerPoint + 0.5f) / pixelsPerPoint;
                    float right = left + snappedSize;
                    float bottom = top + snappedSize;

                    mesh.SetNextVertex(new Vertex { position = new Vector3(left, top, Vertex.nearZ), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(right, top, Vertex.nearZ), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(right, bottom, Vertex.nearZ), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(left, bottom, Vertex.nearZ), tint = tint });

                    ushort v = (ushort)(i * 4);
                    mesh.SetNextIndex(v);
                    mesh.SetNextIndex((ushort)(v + 1));
                    mesh.SetNextIndex((ushort)(v + 2));
                    mesh.SetNextIndex(v);
                    mesh.SetNextIndex((ushort)(v + 2));
                    mesh.SetNextIndex((ushort)(v + 3));
                }
            }
        }

        // The first dot centre whose dot still overlaps the view, which may sit just before the leading edge.
        private static float FirstDotPosition(float origin, float step, float halfSize)
        {
            float first = Mathf.Repeat(origin, step);
            return first - step + halfSize > 0f ? first - step : first;
        }
    }
}
