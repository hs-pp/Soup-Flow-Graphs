using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    public class GridBackgroundElement : VisualElement
    {
        private const int MAX_DOTS_PER_MESH = 16000;

        private static readonly CustomStyleProperty<float> SPACING_PROPERTY = new("--grid-spacing");
        private static readonly CustomStyleProperty<float> MIN_DOT_SPACING_PROPERTY = new("--grid-min-dot-spacing");
        private static readonly CustomStyleProperty<float> DOT_SIZE_PROPERTY = new("--grid-dot-size");
        private static readonly CustomStyleProperty<float> MIN_DOT_SIZE_PROPERTY = new("--grid-min-dot-size");
        private static readonly CustomStyleProperty<Color> DOT_COLOR_PROPERTY = new("--grid-dot-color");

        private readonly GraphView m_graphView;

        private float m_spacing;
        private float m_minDotSpacing;
        private float m_dotSize;
        private float m_minDotSize;
        private Color m_dotColor;

        public GridBackgroundElement(GraphView graphView)
        {
            m_graphView = graphView;

            AddToClassList("gridBackground");
            pickingMode = PickingMode.Ignore;
            this.StretchToParentSize();
            generateVisualContent = OnGenerateVisualContent;

            Add(new GraphAxesElement(graphView));

            RegisterCallback<AttachToPanelEvent>(OnAttachEvent);
            RegisterCallback<DetachFromPanelEvent>(DetachFromPanelEvent);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            ICustomStyle customStyle = e.customStyle;

            if (customStyle.TryGetValue(SPACING_PROPERTY, out float spacing))
            {
                m_spacing = spacing;
            }

            if (customStyle.TryGetValue(MIN_DOT_SPACING_PROPERTY, out float minDotSpacing))
            {
                m_minDotSpacing = minDotSpacing;
            }

            if (customStyle.TryGetValue(DOT_SIZE_PROPERTY, out float dotSize))
            {
                m_dotSize = dotSize;
            }

            if (customStyle.TryGetValue(MIN_DOT_SIZE_PROPERTY, out float minDotSize))
            {
                m_minDotSize = minDotSize;
            }

            if (customStyle.TryGetValue(DOT_COLOR_PROPERTY, out Color dotColor))
            {
                m_dotColor = dotColor;
            }

            MarkDirtyRepaint();
        }

        private void OnAttachEvent(AttachToPanelEvent evt)
        {
            // Listen for Zoom/Pan Changes
            m_graphView.OnViewTransformChanged += RequestRepaint;
        }

        private void DetachFromPanelEvent(DetachFromPanelEvent evt)
        {
            // Stop Listening for Zoom/Pan Changes
            m_graphView.OnViewTransformChanged -= RequestRepaint;
        }

        private void RequestRepaint(GraphElementContainer contentContainer) => MarkDirtyRepaint();

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (m_spacing <= 0f || m_dotSize <= 0f || m_dotColor.a <= 0f)
            {
                return;
            }

            Vector2 size = contentRect.size;
            Vector2 translate = m_graphView.GetViewTransformPosition();
            Vector2 scale = m_graphView.GetViewTransformScale();
            Vector2 offset = m_graphView.ContentContainer.layout.position;

            // Screen-space position of the graph origin, so dots stay aligned to it while panning.
            Vector2 origin = offset * scale + translate;

            // Dots shrink with zoom, down to a floor that keeps them visible when fully zoomed out.
            float dotSize = Mathf.Max(m_minDotSize, m_dotSize * scale.x);

            float minScreenSpacing = Mathf.Max(m_minDotSpacing, dotSize * 2f);
            float stepX = m_spacing * scale.x;
            float stepY = m_spacing * scale.y;
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
            int columns = Mathf.CeilToInt((size.x + halfSize - firstX) / stepX);
            int rows = Mathf.CeilToInt((size.y + halfSize - firstY) / stepY);

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

            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float snappedSize = Mathf.Max(1f, Mathf.Floor(dotSize * pixelsPerPoint + 0.5f)) / pixelsPerPoint;
            Color32 tint = m_dotColor;

            int dotCount = drawnColumns * drawnRows;
            int dot = 0;
            while (dot < dotCount)
            {
                int batch = Mathf.Min(dotCount - dot, MAX_DOTS_PER_MESH);
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

                    ushort firstVertex = (ushort)(i * 4);
                    mesh.SetNextIndex(firstVertex);
                    mesh.SetNextIndex((ushort)(firstVertex + 1));
                    mesh.SetNextIndex((ushort)(firstVertex + 2));
                    mesh.SetNextIndex(firstVertex);
                    mesh.SetNextIndex((ushort)(firstVertex + 2));
                    mesh.SetNextIndex((ushort)(firstVertex + 3));
                }
            }
        }

        private static float FirstDotPosition(float origin, float step, float halfSize)
        {
            float first = Mathf.Repeat(origin, step);
            return first - step + halfSize > 0f ? first - step : first;
        }
    }
}
