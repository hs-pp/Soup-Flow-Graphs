using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    public class PanelEdgeShadowElement : VisualElement
    {
        public enum Edge
        {
            Top,
            Right,
            Bottom,
            Left
        }

        private const string STYLESHEET_PATH = "Soup/PanelEdgeShadowElement";
        private const int GRADIENT_RESOLUTION = 32;

        private static readonly Dictionary<Edge, Texture2D> s_gradients = new();

        public PanelEdgeShadowElement(Edge edge)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLESHEET_PATH));
            AddToClassList("panelEdgeShadow");
            AddToClassList($"panelEdgeShadow{edge}");
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;

            // Pin to the edge and stretch along it; USS sets the depth.
            style.top = edge == Edge.Bottom ? StyleKeyword.Auto : 0f;
            style.bottom = edge == Edge.Top ? StyleKeyword.Auto : 0f;
            style.left = edge == Edge.Right ? StyleKeyword.Auto : 0f;
            style.right = edge == Edge.Left ? StyleKeyword.Auto : 0f;

            style.backgroundImage = GetGradient(edge);
        }

        private static Texture2D GetGradient(Edge edge)
        {
            if (s_gradients.TryGetValue(edge, out Texture2D gradient) && gradient != null)
            {
                return gradient;
            }

            bool vertical = edge is Edge.Top or Edge.Bottom;
            gradient = new Texture2D(vertical ? 1 : GRADIENT_RESOLUTION, vertical ? GRADIENT_RESOLUTION : 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"PanelEdgeShadowGradient{edge}",
                hideFlags = HideFlags.HideAndDontSave,
            };

            for (int i = 0; i < GRADIENT_RESOLUTION; i++)
            {
                float t = i / (GRADIENT_RESOLUTION - 1f);
                if (edge is Edge.Left or Edge.Bottom)
                {
                    t = 1f - t;
                }

                // Quadratic falloff
                Color color = new(1f, 1f, 1f, t * t);
                gradient.SetPixel(vertical ? 0 : i, vertical ? i : 0, color);
            }

            gradient.Apply();
            s_gradients[edge] = gradient;
            return gradient;
        }
    }
}
