using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Edges
{
    public sealed class DefaultEdgeRenderStrategy : IEdgeRenderStrategy
    {
        private const float MIN_EDGE_WIDTH = 1.75f;
        private const float MIN_OPACITY = 0.6f;
        private const float EDGE_LENGTH_FROM_PORT = 12f;
        private const float EDGE_TURN_DIAMETER = 16f;
        private const float EDGE_TURN_RADIUS = EDGE_TURN_DIAMETER / 2f;
        private const float EDGE_WIDTH = 2f;
        private const float INTERCEPT_WIDTH = 6f;

        private const int CORNER_HIT_SAMPLES = 8;

        private static readonly Color SELECTED_COLOR = new(240 / 255f, 240 / 255f, 240 / 255f);
        private static readonly Color UNSELECTED_COLOR = new(146 / 255f, 146 / 255f, 146 / 255f);

        private readonly List<Vector2> m_hitPoints = new();
        private readonly Gradient m_gradient = new();
        private readonly GradientColorKey[] m_colorKeys = new GradientColorKey[2];
        private readonly GradientAlphaKey[] m_alphaKeys = new GradientAlphaKey[1];

        private Vector2[] m_controlPoints;
        private Vector2[] m_localPoints;
        private bool m_controlPointsDirty = true;
        private bool m_localPointsDirty = true;

        private bool m_drawStraight;

        private Color InputColor { get; set; } = UNSELECTED_COLOR;
        private Color OutputColor { get; set; } = UNSELECTED_COLOR;

        public void UpdateLayout(EdgeView edge)
        {
            if (edge.Graph == null)
            {
                return;
            }

            ComputeControlPoints(edge);
            if (m_controlPoints == null || m_controlPoints.Length == 0)
            {
                return;
            }

            Vector2 min = m_controlPoints[0];
            Vector2 max = m_controlPoints[0];
            for (int i = 1; i < m_controlPoints.Length; i++)
            {
                Vector2 point = m_controlPoints[i];
                min.x = Mathf.Min(min.x, point.x);
                min.y = Mathf.Min(min.y, point.y);
                max.x = Mathf.Max(max.x, point.x);
                max.y = Mathf.Max(max.y, point.y);
            }

            float margin = Mathf.Max(EDGE_WIDTH, MIN_EDGE_WIDTH / edge.Graph.CurrentScale);
            edge.SetRenderBounds(new Rect(min.x - margin, min.y - margin, max.x - min.x + margin * 2, max.y - min.y + margin * 2));
        }

        public void SetSelected(EdgeView edge, bool selected)
        {
            InputColor = selected ? SELECTED_COLOR : UNSELECTED_COLOR;
            OutputColor = selected ? SELECTED_COLOR : UNSELECTED_COLOR;
            edge.MarkDirtyRepaint();
        }

        public void Render(EdgeView edge, MeshGenerationContext context)
        {
            if (edge.Graph == null)
            {
                return;
            }

            UpdateLocalPoints(edge);
            if (m_localPoints == null || m_localPoints.Length < 2)
            {
                return;
            }

            float scale = edge.Graph.CurrentScale;
            float width = EDGE_WIDTH;
            float alpha = 1f;
            if (width * scale < MIN_EDGE_WIDTH)
            {
                alpha = Mathf.Lerp(MIN_OPACITY, 1f, width * scale / MIN_EDGE_WIDTH);
                width = MIN_EDGE_WIDTH / scale;
            }

            m_colorKeys[0] = new GradientColorKey(OutputColor, 0);
            m_colorKeys[1] = new GradientColorKey(InputColor, 1);
            m_alphaKeys[0] = new GradientAlphaKey(alpha, 0);
            m_gradient.SetKeys(m_colorKeys, m_alphaKeys);

            Painter2D painter = context.painter2D;
            painter.BeginPath();
            painter.strokeGradient = m_gradient;
            painter.lineWidth = width;
            painter.MoveTo(m_localPoints[0]);

            if (!m_drawStraight)
            {
                for (int i = 1; i < m_localPoints.Length - 1; i++)
                {
                    Vector2 corner = m_localPoints[i];
                    GetCornerTrim(m_localPoints[i - 1], corner, m_localPoints[i + 1], out Vector2 start, out Vector2 end);
                    painter.LineTo(start);
                    painter.BezierCurveTo(corner, corner, end);
                }
            }

            painter.LineTo(m_localPoints[^1]);
            painter.Stroke();
        }

        public void Invalidate()
        {
            m_controlPointsDirty = true;
        }

        public bool ContainsPoint(EdgeView edge, Vector2 localPoint)
        {
            if (edge.Graph == null)
            {
                return false;
            }

            UpdateLocalPoints(edge);
            float interceptWidthSquared = INTERCEPT_WIDTH * INTERCEPT_WIDTH;
            for (int i = 0; i < m_hitPoints.Count - 1; i++)
            {
                Vector2 from = m_hitPoints[i];
                Vector2 to = m_hitPoints[i + 1];
                Vector2 segment = to - from;
                float segmentLengthSquared = segment.sqrMagnitude;
                if (segmentLengthSquared <= 0)
                {
                    continue;
                }

                float t = Mathf.Clamp01(Vector2.Dot(localPoint - from, segment) / segmentLengthSquared);
                if ((localPoint - (from + t * segment)).sqrMagnitude <= interceptWidthSquared)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Overlaps(EdgeView edge, Rect rect)
        {
            if (edge.Graph == null)
            {
                return false;
            }

            UpdateLocalPoints(edge);
            for (int i = 0; i < m_hitPoints.Count - 1; i++)
            {
                if (RectUtils.IntersectsSegment(rect, m_hitPoints[i], m_hitPoints[i + 1]))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateLocalPoints(EdgeView edge)
        {
            ComputeControlPoints(edge);
            if (!m_localPointsDirty || m_controlPoints == null)
            {
                return;
            }

            if (m_localPoints == null || m_localPoints.Length != m_controlPoints.Length)
            {
                m_localPoints = new Vector2[m_controlPoints.Length];
            }

            for (int i = 0; i < m_controlPoints.Length; i++)
            {
                m_localPoints[i] = edge.Graph.ContentContainer.ChangeCoordinatesTo(edge, m_controlPoints[i]);
            }

            m_drawStraight = ShouldDrawStraight(m_localPoints);
            BuildHitPoints();
            m_localPointsDirty = false;
        }

        private static bool ShouldDrawStraight(Vector2[] points)
        {
            if (points.Length != 4)
            {
                return false;
            }

            float stubLength = Vector2.Distance(points[0], points[1]) + Vector2.Distance(points[2], points[3]);
            return Vector2.Distance(points[0], points[3]) < stubLength || Vector2.Distance(points[1], points[2]) < EDGE_TURN_DIAMETER;
        }

        private void BuildHitPoints()
        {
            m_hitPoints.Clear();
            m_hitPoints.Add(m_localPoints[0]);
            if (!m_drawStraight)
            {
                for (int i = 1; i < m_localPoints.Length - 1; i++)
                {
                    Vector2 corner = m_localPoints[i];
                    GetCornerTrim(m_localPoints[i - 1], corner, m_localPoints[i + 1], out Vector2 start, out Vector2 end);
                    for (int sample = 0; sample < CORNER_HIT_SAMPLES; sample++)
                    {
                        float t = sample / (float)(CORNER_HIT_SAMPLES - 1);
                        float u = 1 - t;

                        // Matches the cubic Render() draws, whose two control points both sit on the corner.
                        m_hitPoints.Add(u * u * u * start + 3 * u * t * corner + t * t * t * end);
                    }
                }
            }

            m_hitPoints.Add(m_localPoints[^1]);
        }

        private static void GetCornerTrim(Vector2 previous, Vector2 corner, Vector2 next, out Vector2 start, out Vector2 end)
        {
            Vector2 toPrevious = previous - corner;
            Vector2 toNext = next - corner;

            float radius = Mathf.Min(EDGE_TURN_RADIUS, Mathf.Min(toPrevious.magnitude, toNext.magnitude) * 0.5f);
            start = corner + toPrevious.normalized * radius;
            end = corner + toNext.normalized * radius;
        }

        private void ComputeControlPoints(EdgeView edge)
        {
            if (!m_controlPointsDirty)
            {
                return;
            }

            float distance = (edge.To - edge.From).magnitude;
            float offset = Mathf.Max(Mathf.Min(EDGE_LENGTH_FROM_PORT + EDGE_TURN_DIAMETER, distance * 2f), EDGE_TURN_DIAMETER);

            bool reversed = edge.From.x > edge.To.x;
            int pointCount = reversed ? 6 : 4;
            if (m_controlPoints == null || m_controlPoints.Length != pointCount)
            {
                m_controlPoints = new Vector2[pointCount];
            }

            m_controlPoints[0] = edge.From;
            m_controlPoints[^1] = edge.To;
            float fromX = edge.OutputOrientation == Orientation.Horizontal ? edge.From.x + offset : edge.From.x;
            float fromY = edge.OutputOrientation == Orientation.Horizontal ? edge.From.y : edge.From.y + offset;
            float toX = edge.InputOrientation == Orientation.Horizontal ? edge.To.x - offset : edge.To.x;
            float toY = edge.InputOrientation == Orientation.Horizontal ? edge.To.y : edge.To.y - offset;
            m_controlPoints[1] = new Vector2(fromX, fromY);
            m_controlPoints[^2] = new Vector2(toX, toY);

            if (reversed)
            {
                float middleY = (edge.From.y + edge.To.y) / 2f;
                PortView input = edge.InputPort;
                PortView output = edge.OutputPort;
                if (input != null && output != null)
                {
                    PortView highestPort = input.GetGlobalCenter().y < output.GetGlobalCenter().y ? input : output;
                    PortView lowestPort = highestPort == input ? output : input;
                    GraphElement highestNode = highestPort.GetFirstAncestorOfType<GraphElement>();
                    GraphElement lowestNode = lowestPort.GetFirstAncestorOfType<GraphElement>();
                    if (highestNode != null && lowestNode != null)
                    {
                        middleY = (highestNode.GetPosition().y + highestNode.resolvedStyle.height + lowestNode.GetPosition().y) / 2f;
                        middleY = Mathf.Clamp(middleY, highestNode.GetPosition().y + 20, lowestNode.GetPosition().y + 40);
                    }
                }

                m_controlPoints[2] = new Vector2(fromX, middleY);
                m_controlPoints[3] = new Vector2(toX, middleY);
            }

            m_controlPointsDirty = false;
            m_localPointsDirty = true;
        }
    }
}
