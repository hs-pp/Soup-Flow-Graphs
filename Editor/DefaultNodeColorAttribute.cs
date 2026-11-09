using System;
using UnityEngine;

namespace Soup.Editor
{
    /// <summary>
    /// Specify the default node color on a NodeView.
    /// Add this to a class inheriting BaseNodeView.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class DefaultNodeColorAttribute : Attribute
    {
        public string Hex { get; }
        public Color Color { get; }
        public bool IsValid { get; }

        /// <summary>
        /// "#RRGGBB" or "#RRGGBBAA" format.
        /// </summary>
        public DefaultNodeColorAttribute(string hex)
        {
            Hex = hex;
            if (hex != null && hex.StartsWith("#", StringComparison.Ordinal) && ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                Color = color;
                IsValid = true;
            }
        }
    }
}
