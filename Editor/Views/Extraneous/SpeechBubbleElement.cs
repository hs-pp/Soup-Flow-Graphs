using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    public enum SpeechBubbleArrowSide
    {
        Bottom,
        Top,
    }

    /// <summary>
    /// A rounded box with a small arrow on its top or bottom edge, styled through the --bubble-* custom properties.
    /// Line width and corner radius come from border-top-width and border-top-left-radius; keep the USS border color
    /// transparent so only the painted outline shows. The arrow is drawn outside the layout rect, so leave at least
    /// --bubble-arrow-height of margin on that side.
    /// </summary>
    [UxmlElement]
    public partial class SpeechBubbleElement : VisualElement
    {
        private static readonly CustomStyleProperty<Color> s_fillColorProperty = new("--bubble-fill-color");
        private static readonly CustomStyleProperty<Color> s_borderColorProperty = new("--bubble-border-color");
        private static readonly CustomStyleProperty<float> s_arrowOffsetProperty = new("--bubble-arrow-offset");
        private static readonly CustomStyleProperty<float> s_arrowWidthProperty = new("--bubble-arrow-width");
        private static readonly CustomStyleProperty<float> s_arrowHeightProperty = new("--bubble-arrow-height");

        private SpeechBubbleArrowSide m_arrowSide;
        private Color m_fillColor;
        private Color m_borderColor;
        private float m_arrowOffset;
        private float m_arrowWidth;
        private float m_arrowHeight;

        [UxmlAttribute]
        public SpeechBubbleArrowSide ArrowSide
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
            generateVisualContent = OnGenerateBubble;
            RegisterCallback<CustomStyleResolvedEvent>(OnBubbleStyleResolved);
        }

        private void OnBubbleStyleResolved(CustomStyleResolvedEvent e)
        {
            ICustomStyle customStyle = e.customStyle;

            if (customStyle.TryGetValue(s_fillColorProperty, out Color fillColor))
            {
                m_fillColor = fillColor;
            }

            if (customStyle.TryGetValue(s_borderColorProperty, out Color borderColor))
            {
                m_borderColor = borderColor;
            }

            if (customStyle.TryGetValue(s_arrowOffsetProperty, out float arrowOffset))
            {
                m_arrowOffset = arrowOffset;
            }

            if (customStyle.TryGetValue(s_arrowWidthProperty, out float arrowWidth))
            {
                m_arrowWidth = arrowWidth;
            }

            if (customStyle.TryGetValue(s_arrowHeightProperty, out float arrowHeight))
            {
                m_arrowHeight = arrowHeight;
            }

            MarkDirtyRepaint();
        }

        private void OnGenerateBubble(MeshGenerationContext ctx)
        {
            float lineWidth = resolvedStyle.borderTopWidth;
            float inset = lineWidth * 0.5f;
            float left = inset;
            float top = inset;
            float right = layout.width - inset;
            float bottom = layout.height - inset;
            float radius = Mathf.Max(0.0f, resolvedStyle.borderTopLeftRadius - inset);
            float arrowLeft = Mathf.Max(left + radius, Mathf.Min(m_arrowOffset, right - radius - m_arrowWidth));
            float arrowMid = arrowLeft + m_arrowWidth * 0.5f;
            float arrowRight = arrowLeft + m_arrowWidth;

            Painter2D painter = ctx.painter2D;
            painter.lineWidth = lineWidth;
            painter.lineJoin = LineJoin.Round;
            painter.fillColor = m_fillColor;
            painter.strokeColor = m_borderColor;
            painter.BeginPath();
            painter.MoveTo(new(left + radius, top));
            if (m_arrowSide == SpeechBubbleArrowSide.Top)
            {
                painter.LineTo(new(arrowLeft, top));
                painter.LineTo(new(arrowMid, top - m_arrowHeight));
                painter.LineTo(new(arrowRight, top));
            }
            painter.ArcTo(new(right, top), new(right, bottom), radius);
            painter.ArcTo(new(right, bottom), new(left, bottom), radius);
            if (m_arrowSide == SpeechBubbleArrowSide.Bottom)
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
