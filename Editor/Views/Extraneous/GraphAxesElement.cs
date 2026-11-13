using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    public class GraphAxesElement : VisualElement
    {
        private const int MAX_TICKS_PER_AXIS = 200;

        private static readonly float[] MANTISSAS = { 1f, 2f, 5f };

        private static readonly CustomStyleProperty<float> AXIS_THICKNESS_PROPERTY = new("--graph-axis-thickness");
        private static readonly CustomStyleProperty<float> MIN_TICK_SPACING_PROPERTY = new("--graph-axis-min-tick-spacing");
        private static readonly CustomStyleProperty<float> LABEL_WIDTH_PROPERTY = new("--coordinate-label-width");
        private static readonly CustomStyleProperty<float> LABEL_HEIGHT_PROPERTY = new("--coordinate-label-height");
        private static readonly CustomStyleProperty<float> LABEL_GAP_PROPERTY = new("--coordinate-label-gap");
        private static readonly CustomStyleProperty<float> TICK_LENGTH_PROPERTY = new("--coordinate-tick-length");
        private static readonly CustomStyleProperty<float> TICK_THICKNESS_PROPERTY = new("--coordinate-tick-thickness");

        private readonly GraphView m_graphView;
        
        private readonly VisualElement m_verticalAxis;
        private readonly VisualElement m_horizontalAxis;
        private readonly List<CoordinateLabel> m_labels = new();
        private int m_activeLabels;

        private float m_axisThickness;
        private float m_minTickSpacing;
        private float m_labelWidth;
        private float m_labelHeight;
        private float m_labelGap;
        private float m_tickLength;
        private float m_tickThickness;

        public GraphAxesElement(GraphView graphView)
        {
            m_graphView = graphView;

            name = "graph-axes";
            AddToClassList("graphAxes");
            pickingMode = PickingMode.Ignore;
            this.StretchToParentSize();

            // Maybe pull this out into its own UXML and USS one day.
            m_verticalAxis = new VisualElement { name = "graph-axis-vertical", pickingMode = PickingMode.Ignore };
            m_verticalAxis.AddToClassList("graphAxis");
            m_verticalAxis.style.position = Position.Absolute;
            m_verticalAxis.style.left = 0f;
            m_verticalAxis.style.top = 0f;
            m_verticalAxis.style.height = Length.Percent(100f);

            m_horizontalAxis = new VisualElement { name = "graph-axis-horizontal", pickingMode = PickingMode.Ignore };
            m_horizontalAxis.AddToClassList("graphAxis");
            m_horizontalAxis.style.position = Position.Absolute;
            m_horizontalAxis.style.left = 0f;
            m_horizontalAxis.style.top = 0f;
            m_horizontalAxis.style.width = Length.Percent(100f);

            Add(m_verticalAxis);
            Add(m_horizontalAxis);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            ICustomStyle customStyle = e.customStyle;

            if (customStyle.TryGetValue(AXIS_THICKNESS_PROPERTY, out float axisThickness))
            {
                m_axisThickness = axisThickness;
            }

            if (customStyle.TryGetValue(MIN_TICK_SPACING_PROPERTY, out float minTickSpacing))
            {
                m_minTickSpacing = minTickSpacing;
            }

            if (customStyle.TryGetValue(LABEL_WIDTH_PROPERTY, out float labelWidth))
            {
                m_labelWidth = labelWidth;
            }

            if (customStyle.TryGetValue(LABEL_HEIGHT_PROPERTY, out float labelHeight))
            {
                m_labelHeight = labelHeight;
            }

            if (customStyle.TryGetValue(LABEL_GAP_PROPERTY, out float labelGap))
            {
                m_labelGap = labelGap;
            }

            if (customStyle.TryGetValue(TICK_LENGTH_PROPERTY, out float tickLength))
            {
                m_tickLength = tickLength;
            }

            if (customStyle.TryGetValue(TICK_THICKNESS_PROPERTY, out float tickThickness))
            {
                m_tickThickness = tickThickness;
            }

            m_verticalAxis.style.width = m_axisThickness;
            m_horizontalAxis.style.height = m_axisThickness;

            foreach (CoordinateLabel label in m_labels)
            {
                ApplyMetrics(label);
            }

            Refresh();
        }

        private void ApplyMetrics(CoordinateLabel label)
        {
            label.SetMetrics(m_labelWidth, m_labelHeight, m_labelGap, m_tickLength, m_tickThickness, m_axisThickness * 0.5f);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_graphView.OnViewTransformChanged += OnViewTransformChanged;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_graphView.OnViewTransformChanged -= OnViewTransformChanged;
        }

        private void OnViewTransformChanged(GraphElementContainer contentContainer) => Refresh();

        private void OnGeometryChanged(GeometryChangedEvent evt) => Refresh();

        #region Coordinate conversion
        private static Vector2 GraphToScreen(Vector2 graph, Vector2 offset, float scale, Vector2 translate)
        {
            return (graph + offset) * scale + translate;
        }

        private static Vector2 ScreenToGraph(Vector2 screen, Vector2 offset, float scale, Vector2 translate)
        {
            return (screen - translate) / scale - offset;
        }
        #endregion

        private void Refresh()
        {
            if (m_axisThickness <= 0f || m_minTickSpacing <= 0f)
            {
                HideSurplusLabels(0);
                return;
            }

            Vector2 size = contentRect.size;
            float scale = m_graphView.GetViewTransformScale().x;
            Vector2 translate = m_graphView.GetViewTransformPosition();
            Vector2 offset = m_graphView.ContentContainer.layout.position;

            if (!float.IsFinite(size.x) || !float.IsFinite(size.y) || size.x <= 0f || size.y <= 0f || !float.IsFinite(scale) || scale <= 0f || !float.IsFinite(translate.x) || !float.IsFinite(translate.y))
            {
                HideSurplusLabels(0);
                return;
            }

            if (!float.IsFinite(offset.x) || !float.IsFinite(offset.y))
            {
                offset = Vector2.zero;
            }

            m_activeLabels = 0;

            Vector2 origin = GraphToScreen(Vector2.zero, offset, scale, translate);
            Vector2 graphMin = ScreenToGraph(Vector2.zero, offset, scale, translate);
            Vector2 graphMax = ScreenToGraph(size, offset, scale, translate);

            float maxPinnedX = Mathf.Max(0f, size.x - m_axisThickness);
            float maxPinnedY = Mathf.Max(0f, size.y - m_axisThickness);
            float lineX = origin.x - m_axisThickness * 0.5f;
            float lineY = origin.y - m_axisThickness * 0.5f;

            m_verticalAxis.style.translate = new Translate(lineX, 0f);
            m_horizontalAxis.style.translate = new Translate(0f, lineY);

            float pinnedX = Mathf.Clamp(lineX, 0f, maxPinnedX);
            float pinnedY = Mathf.Clamp(lineY, 0f, maxPinnedY);
            bool verticalPinned = !Mathf.Approximately(pinnedX, lineX);
            bool horizontalPinned = !Mathf.Approximately(pinnedY, lineY);

            float increment = ChooseIncrement(scale);

            CoordinateLabel.Direction verticalDirection = pinnedX >= m_labelGap + m_labelWidth ? CoordinateLabel.Direction.Left : CoordinateLabel.Direction.Right;
            PlaceTicks(true, graphMin.y, graphMax.y, increment, offset.y, scale, translate.y, pinnedX + m_axisThickness * 0.5f, verticalDirection, !verticalPinned);

            CoordinateLabel.Direction horizontalDirection = pinnedY >= m_labelGap + m_labelHeight ? CoordinateLabel.Direction.Up : CoordinateLabel.Direction.Down;
            PlaceTicks(false, graphMin.x, graphMax.x, increment, offset.x, scale, translate.x, pinnedY + m_axisThickness * 0.5f, horizontalDirection, !horizontalPinned);

            HideSurplusLabels(m_activeLabels);
        }

        private void PlaceTicks(bool vertical, float graphMin, float graphMax, float increment, float offsetAlong, float scale, float translateAlong, float across, CoordinateLabel.Direction direction, bool showTicks)
        {
            int first = Mathf.CeilToInt(graphMin / increment);
            int last = Mathf.FloorToInt(graphMax / increment);
            if (last - first > MAX_TICKS_PER_AXIS)
            {
                return;
            }

            for (int i = first; i <= last; i++)
            {
                if (i == 0)
                {
                    continue; // Zero is where the axes cross, so it gets no label.
                }

                float graph = i * increment;
                float along = (graph + offsetAlong) * scale + translateAlong;
                Vector2 anchor = vertical ? new Vector2(across, along) : new Vector2(along, across);
                NextLabel().SetLabel(graph, anchor, direction, showTicks);
            }
        }

        private float ChooseIncrement(float scale)
        {
            float minimum = m_minTickSpacing / scale;
            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(minimum)));

            foreach (float mantissa in MANTISSAS)
            {
                float candidate = mantissa * magnitude;
                if (candidate >= minimum)
                {
                    return candidate;
                }
            }

            return 10f * magnitude;
        }
        
        #region Label pool
        private CoordinateLabel NextLabel()
        {
            if (m_activeLabels == m_labels.Count)
            {
                CoordinateLabel created = new();
                ApplyMetrics(created);
                m_labels.Add(created);
                Add(created);
            }

            CoordinateLabel label = m_labels[m_activeLabels++];
            label.style.display = DisplayStyle.Flex;
            return label;
        }

        private void HideSurplusLabels(int keep)
        {
            for (int i = keep; i < m_labels.Count; i++)
            {
                m_labels[i].style.display = DisplayStyle.None;
            }

            m_activeLabels = keep;
        }
        #endregion

        private class CoordinateLabel : VisualElement
        {
            public enum Direction
            {
                Up,
                Down,
                Left,
                Right
            }

            private readonly Label m_text;
            private readonly VisualElement m_tick;

            private float m_labelWidth;
            private float m_labelHeight;
            private float m_labelGap;
            private float m_tickLength;
            private float m_tickThickness;
            private float m_axisHalfThickness;

            public CoordinateLabel()
            {
                AddToClassList("coordinateLabel");
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.top = 0f;

                m_tick = new() { pickingMode = PickingMode.Ignore };
                m_tick.AddToClassList("coordinateLabelTick");
                m_tick.style.position = Position.Absolute;
                m_tick.style.left = 0f;
                m_tick.style.top = 0f;

                m_text = new Label { pickingMode = PickingMode.Ignore };
                m_text.AddToClassList("coordinateLabelText");
                m_text.style.position = Position.Absolute;
                m_text.style.left = 0f;
                m_text.style.top = 0f;

                Add(m_tick);
                Add(m_text);
            }

            public void SetMetrics(float labelWidth, float labelHeight, float labelGap, float tickLength, float tickThickness, float axisHalfThickness)
            {
                m_labelWidth = labelWidth;
                m_labelHeight = labelHeight;
                m_labelGap = labelGap;
                m_tickLength = tickLength;
                m_tickThickness = tickThickness;
                m_axisHalfThickness = axisHalfThickness;

                m_text.style.width = labelWidth;
                m_text.style.height = labelHeight;
            }

            public void SetLabel(float coordinate, Vector2 anchor, Direction direction, bool showTick)
            {
                if (!float.IsFinite(anchor.x) || !float.IsFinite(anchor.y))
                {
                    style.display = DisplayStyle.None;
                    return;
                }

                m_text.text = coordinate.ToString("0.###");
                m_tick.style.display = showTick ? DisplayStyle.Flex : DisplayStyle.None;

                float edge = m_axisHalfThickness;
                bool horizontalAxis = direction is Direction.Up or Direction.Down;
                m_tick.style.width = horizontalAxis ? m_tickThickness : m_tickLength;
                m_tick.style.height = horizontalAxis ? m_tickLength : m_tickThickness;

                Vector2 text;
                Vector2 tick;
                switch (direction)
                {
                    case Direction.Up:
                        text = new Vector2(anchor.x - m_labelWidth * 0.5f, anchor.y - edge - m_labelGap - m_labelHeight);
                        tick = new Vector2(anchor.x - m_tickThickness * 0.5f, anchor.y + edge);
                        m_text.style.unityTextAlign = TextAnchor.LowerCenter;
                        break;
                    case Direction.Down:
                        text = new Vector2(anchor.x - m_labelWidth * 0.5f, anchor.y + edge + m_labelGap);
                        tick = new Vector2(anchor.x - m_tickThickness * 0.5f, anchor.y - edge - m_tickLength);
                        m_text.style.unityTextAlign = TextAnchor.UpperCenter;
                        break;
                    case Direction.Left:
                        text = new Vector2(anchor.x - edge - m_labelGap - m_labelWidth, anchor.y - m_labelHeight * 0.5f);
                        tick = new Vector2(anchor.x + edge, anchor.y - m_tickThickness * 0.5f);
                        m_text.style.unityTextAlign = TextAnchor.MiddleRight;
                        break;
                    default:
                        text = new Vector2(anchor.x + edge + m_labelGap, anchor.y - m_labelHeight * 0.5f);
                        tick = new Vector2(anchor.x - edge - m_tickLength, anchor.y - m_tickThickness * 0.5f);
                        m_text.style.unityTextAlign = TextAnchor.MiddleLeft;
                        break;
                }

                m_text.style.translate = new Translate(text.x, text.y);
                m_tick.style.translate = new Translate(tick.x, tick.y);
            }
        }
    }
}
