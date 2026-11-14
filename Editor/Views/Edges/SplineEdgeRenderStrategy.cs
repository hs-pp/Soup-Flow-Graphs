using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Edges
{
    /// <summary>
    /// Designed to behave similar to Unreal's blueprints edges.
    /// </summary>
    public sealed class SplineEdgeRenderStrategy : IEdgeRenderStrategy
    {
        private const float MIN_EDGE_WIDTH = 1.75f;
        private const float MIN_OPACITY = 0.6f;
        private const float EDGE_WIDTH = 2f;
        private const float INTERCEPT_WIDTH = 6f;

        private const float FORWARD_DELTA_RANGE = 1000f;
        private const float FORWARD_TANGENT_FROM_ALONG = 1f;
        private const float FORWARD_TANGENT_FROM_ACROSS = 1f;
        private const float BACKWARD_DELTA_RANGE = 200f;
        private const float BACKWARD_TANGENT_FROM_ALONG = 2f;
        private const float BACKWARD_TANGENT_FROM_ACROSS = 1.5f;

        private const float HIT_SEGMENT_LENGTH = 12f;
        private const int MIN_HIT_SEGMENTS = 8;
        private const int MAX_HIT_SEGMENTS = 64;

        private static readonly Color SELECTED_COLOR = new(240 / 255f, 240 / 255f, 240 / 255f);
        private static readonly Color UNSELECTED_COLOR = new(146 / 255f, 146 / 255f, 146 / 255f);

        private readonly List<Vector2> m_hitPoints = new();
        private readonly Gradient m_gradient = new();
        private readonly GradientColorKey[] m_colorKeys = new GradientColorKey[2];
        private readonly GradientAlphaKey[] m_alphaKeys = new GradientAlphaKey[1];

        private readonly Vector2[] m_controlPoints = new Vector2[4];
        private readonly Vector2[] m_localPoints = new Vector2[4];
        private bool m_controlPointsDirty = true;
        private bool m_localPointsDirty = true;

        private Color InputColor { get; set; } = UNSELECTED_COLOR;
        private Color OutputColor { get; set; } = UNSELECTED_COLOR;

        public void UpdateLayout(EdgeView edge)
        {
            if (edge.Graph == null)
            {
                return;
            }

            ComputeControlPoints(edge);

            Vector2 min = m_controlPoints[0];
            Vector2 max = m_controlPoints[0];
            for (int i = 1; i < m_controlPoints.Length; i++)
            {
                min = Vector2.Min(min, m_controlPoints[i]);
                max = Vector2.Max(max, m_controlPoints[i]);
            }

            // Wide enough for the hit band, not just the line, since UI Toolkit only picks inside an element's bounds.
            float margin = Mathf.Max(INTERCEPT_WIDTH, MIN_EDGE_WIDTH / edge.Graph.CurrentScale);
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
            painter.BezierCurveTo(m_localPoints[1], m_localPoints[2], m_localPoints[3]);
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
            if (!m_localPointsDirty)
            {
                return;
            }

            for (int i = 0; i < m_controlPoints.Length; i++)
            {
                m_localPoints[i] = edge.Graph.ContentContainer.ChangeCoordinatesTo(edge, m_controlPoints[i]);
            }

            BuildHitPoints();
            m_localPointsDirty = false;
        }

        private void BuildHitPoints()
        {
            Vector2 p0 = m_localPoints[0];
            Vector2 p1 = m_localPoints[1];
            Vector2 p2 = m_localPoints[2];
            Vector2 p3 = m_localPoints[3];

            float polygonLength = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3);
            int segments = Mathf.Clamp(Mathf.CeilToInt(polygonLength / HIT_SEGMENT_LENGTH), MIN_HIT_SEGMENTS, MAX_HIT_SEGMENTS);

            m_hitPoints.Clear();
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float u = 1 - t;
                m_hitPoints.Add(u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3);
            }
        }

        private void ComputeControlPoints(EdgeView edge)
        {
            if (!m_controlPointsDirty)
            {
                return;
            }

            Vector2 from = edge.OutputPosition;
            Vector2 to = edge.InputPosition;
            Vector2 delta = to - from;
            Vector2 fromDirection = GetFlowDirection(edge.OutputOrientation);
            Vector2 toDirection = GetFlowDirection(edge.InputOrientation);

            m_controlPoints[0] = from;
            m_controlPoints[1] = from + fromDirection * (ComputeTangentLength(delta, fromDirection) / 3f);
            m_controlPoints[2] = to - toDirection * (ComputeTangentLength(delta, toDirection) / 3f);
            m_controlPoints[3] = to;

            m_controlPointsDirty = false;
            m_localPointsDirty = true;
        }

        private static Vector2 GetFlowDirection(Orientation orientation)
        {
            return orientation == Orientation.Horizontal ? new Vector2(1, 0) : new Vector2(0, 1);
        }

        private static float ComputeTangentLength(Vector2 delta, Vector2 direction)
        {
            float along = Vector2.Dot(delta, direction);
            float across = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
            float forwardAcross = Mathf.Min(across, FORWARD_DELTA_RANGE) * FORWARD_TANGENT_FROM_ACROSS;
            if (along >= 0)
            {
                return Mathf.Min(along, FORWARD_DELTA_RANGE) * FORWARD_TANGENT_FROM_ALONG + forwardAcross;
            }

            float backwardAcross = Mathf.Min(across, BACKWARD_DELTA_RANGE) * BACKWARD_TANGENT_FROM_ACROSS;
            float blend = Mathf.SmoothStep(0f, 1f, -along / BACKWARD_DELTA_RANGE);
            return Mathf.Min(-along, BACKWARD_DELTA_RANGE) * BACKWARD_TANGENT_FROM_ALONG + Mathf.Lerp(forwardAcross, backwardAcross, blend);
        }
    }
}
