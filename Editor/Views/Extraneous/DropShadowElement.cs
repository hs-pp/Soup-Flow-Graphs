using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Extraneous
{
    /// <summary>
    /// A soft drop shadow behind its parent, faking the box-shadow that USS lacks. The shadow is one
    /// 9-sliced texture of a blurred rounded rectangle, so it fits any size and keeps rounded corners.
    /// Blur, corner radius and offset come from DropShadowElement.uss custom properties; colour and
    /// strength come from -unity-background-image-tint-color. Insert it first so it draws behind its siblings.
    /// </summary>
    public class DropShadowElement : VisualElement
    {
        private const string STYLESHEET_PATH = "Soup/DropShadowElement";

        private static readonly CustomStyleProperty<float> s_blurProperty = new("--drop-shadow-blur");
        private static readonly CustomStyleProperty<float> s_radiusProperty = new("--drop-shadow-radius");
        private static readonly CustomStyleProperty<float> s_offsetXProperty = new("--drop-shadow-offset-x");
        private static readonly CustomStyleProperty<float> s_offsetYProperty = new("--drop-shadow-offset-y");

        // Keyed by geometry so a USS override on one shadow cannot hand its texture to every other.
        private static readonly Dictionary<(int, int), Texture2D> s_textures = new();

        private int m_blur = -1;
        private int m_radius = -1;
        private float m_offsetX = float.NaN;
        private float m_offsetY = float.NaN;

        public DropShadowElement()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLESHEET_PATH));
            AddToClassList("dropShadow");
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            ICustomStyle customStyle = e.customStyle;
            customStyle.TryGetValue(s_blurProperty, out float blurValue);
            customStyle.TryGetValue(s_radiusProperty, out float radiusValue);
            customStyle.TryGetValue(s_offsetXProperty, out float offsetX);
            customStyle.TryGetValue(s_offsetYProperty, out float offsetY);

            int blur = Mathf.Max(1, Mathf.RoundToInt(blurValue));
            int radius = Mathf.Max(0, Mathf.RoundToInt(radiusValue));

            if (blur != m_blur || radius != m_radius)
            {
                m_blur = blur;
                m_radius = radius;

                style.backgroundImage = GetTexture(blur, radius, out int border);
                style.unitySliceLeft = border;
                style.unitySliceTop = border;
                style.unitySliceRight = border;
                style.unitySliceBottom = border;
                m_offsetX = float.NaN; // The extents below depend on blur too.
            }

            if (!offsetX.Equals(m_offsetX) || !offsetY.Equals(m_offsetY))
            {
                m_offsetX = offsetX;
                m_offsetY = offsetY;

                // The texture pads the shape by the blur on every side, so overhang the parent by as much.
                style.left = -blur + offsetX;
                style.right = -blur - offsetX;
                style.top = -blur + offsetY;
                style.bottom = -blur - offsetY;
            }
        }

        /// <summary>
        /// White rounded rectangle blurred like a CSS box-shadow (blur = 2 sigma), padded by the blur on
        /// every side so the falloff fits, with a small uniform centre for the 9-slice to stretch.
        /// </summary>
        private static Texture2D GetTexture(int blur, int radius, out int border)
        {
            // The rounded corner's falloff reaches blur past the arc, which itself starts blur in from the
            // texture edge; past that, rows and columns are uniform and safe to stretch.
            border = 2 * blur + radius;

            // The null check also covers a texture destroyed by a domain reload.
            if (s_textures.TryGetValue((blur, radius), out Texture2D texture) && texture != null)
            {
                return texture;
            }

            int size = 2 * border + 2;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"DropShadow_b{blur}_r{radius}",
                hideFlags = HideFlags.HideAndDontSave,
            };

            float sigma = blur * 0.5f;
            float halfExtent = size * 0.5f - blur;
            Vector2 centre = new(size * 0.5f, size * 0.5f);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = RoundedRectDistance(new Vector2(x + 0.5f, y + 0.5f) - centre, halfExtent, radius);
                    float alpha = 0.5f * (1f - Erf(distance / (sigma * Mathf.Sqrt(2f))));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            s_textures[(blur, radius)] = texture;
            return texture;
        }

        // Signed distance from a point (relative to the centre) to a square with rounded corners; negative inside.
        private static float RoundedRectDistance(Vector2 point, float halfExtent, float radius)
        {
            Vector2 q = new(Mathf.Abs(point.x) - (halfExtent - radius), Mathf.Abs(point.y) - (halfExtent - radius));
            Vector2 outside = Vector2.Max(q, Vector2.zero);
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        // Close approximation of the error function; plenty for an 8-bit falloff.
        private static float Erf(float x)
        {
            return (float)System.Math.Tanh(x * (1.1283791671f + 0.1009f * x * x));
        }
    }
}
