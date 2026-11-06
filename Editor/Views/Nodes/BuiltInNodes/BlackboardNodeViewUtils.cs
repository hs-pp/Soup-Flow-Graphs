using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    internal static class BlackboardNodeViewUtils
    {
        public const string MISSING_VARIABLE_TEXT = "Missing variable";

        private const string WARNING_CLASS = "nodeWarningLabel";
        private const string WARNING_PREFIX = "⚠ ";

        public static void SetSummary(Label label, string text, bool isWarning)
        {
            label.text = isWarning ? WARNING_PREFIX + text : text;
            label.EnableInClassList(WARNING_CLASS, isWarning);
        }
    }
}
