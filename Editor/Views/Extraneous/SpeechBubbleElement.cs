using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    [UxmlElement]
    public partial class SpeechBubbleElement : VisualElement
    {
        public enum Side
        {
            Bottom,
            Top,
        }

        private static readonly CustomStyleProperty<Color> FILL_COLOR_PROPERTY = new("--bubble-fill-color");
        private static readonly CustomStyleProperty<Color> BORDER_COLOR_PROPERTY = new("--bubble-border-color");
        private static readonly CustomStyleProperty<float> ARROW_OFFSET_PROPERTY = new("--bubble-arrow-offset");
        private static readonly CustomStyleProperty<float> ARROW_WIDTH_PROPERTY = new("--bubble-arrow-width");
        private static readonly CustomStyleProperty<float> ARROW_HEIGHT_PROPERTY = new("--bubble-arrow-height");

        private Side m_arrowSide;
        private Color m_fillColor;
        private Color m_borderColor;
        private float m_arrowOffset;
        private float m_arrowWidth;
        private float m_arrowHeight;

        [UxmlAttribute]
        public Side ArrowSide
        {
            get => m_arrowSide;
            set
            {
                if (m_arrowSide == value)
                {
                    return;
                }

                m_arrowSide = value;
                MarkDirtyRepaint();
            }
        }

        public SpeechBubbleElement()
        {
            generateVisualContent = OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            ICustomStyle customStyle = e.customStyle;

            if (customStyle.TryGetValue(FILL_COLOR_PROPERTY, out Color fillColor))
            {
                m_fillColor = fillColor;
            }

            if (customStyle.TryGetValue(BORDER_COLOR_PROPERTY, out Color borderColor))
            {
                m_borderColor = borderColor;
            }

            if (customStyle.TryGetValue(ARROW_OFFSET_PROPERTY, out float arrowOffset))
            {
                m_arrowOffset = arrowOffset;
            }

            if (customStyle.TryGetValue(ARROW_WIDTH_PROPERTY, out float arrowWidth))
            {
                m_arrowWidth = arrowWidth;
            }

            if (customStyle.TryGetValue(ARROW_HEIGHT_PROPERTY, out float arrowHeight))
            {
                m_arrowHeight = arrowHeight;
            }

            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            float lineWidth = resolvedStyle.borderTopWidth;
            float inset = lineWidth * 0.5f;
            float left = inset;
            float top = inset;
            float right = layout.width - inset;
            float bottom = layout.height - inset;
            float radius = Mathf.Max(0.0f, resolvedStyle.borderTopLeftRadius - inset);
            float arrowWidth = Mathf.Min(m_arrowWidth, right - left - 2.0f * radius);
            float arrowLeft = Mathf.Clamp(m_arrowOffset, left + radius, right - radius - arrowWidth);
            float arrowMid = arrowLeft + arrowWidth * 0.5f;
            float arrowRight = arrowLeft + arrowWidth;

            Painter2D painter = ctx.painter2D;
            painter.lineWidth = lineWidth;
            painter.lineJoin = LineJoin.Round;
            painter.fillColor = m_fillColor;
            painter.strokeColor = m_borderColor;
            painter.BeginPath();
            painter.MoveTo(new(left + radius, top));
            if (m_arrowSide == Side.Top)
            {
                painter.LineTo(new(arrowLeft, top));
                painter.LineTo(new(arrowMid, top - m_arrowHeight));
                painter.LineTo(new(arrowRight, top));
            }
            painter.ArcTo(new(right, top), new(right, bottom), radius);
            painter.ArcTo(new(right, bottom), new(left, bottom), radius);
            if (m_arrowSide == Side.Bottom)
            {
                painter.LineTo(new(arrowRight, bottom));
                painter.LineTo(new(arrowMid, bottom + m_arrowHeight));
                painter.LineTo(new(arrowLeft, bottom));
            }
            painter.ArcTo(new(left, bottom), new(left, top), radius);
            painter.ArcTo(new(left, top), new(right, top), radius);
            painter.ClosePath();
            painter.Fill();
            if (lineWidth > 0.0f)
            {
                painter.Stroke();
            }
        }
    }
}
