using System;
using Soup.Editor.Core.DataRefs;

namespace Soup.Editor.Views.Inspector.Blackboards
{
    internal static class BlackboardInspectorUtils
    {
        public static string GetElementId(NodeRef nodeRef, string keyFieldName)
        {
            return new BlackboardKeyRef(nodeRef.NodeSP?.FindPropertyRelative(keyFieldName)).ElementId;
        }

        /// <summary>
        /// Why a node's element can't be used, or null when it can. Missing strategy types are reported by the caller.
        /// </summary>
        public static string GetElementProblem(BlackboardRef blackboard, string elementId, out BlackboardElementRef element, out Type valueType)
        {
            element = default;
            valueType = null;
            if (string.IsNullOrEmpty(elementId))
            {
                return "Pick a variable.";
            }

            if (!blackboard.TryGetElement(elementId, out element))
            {
                return "The variable was deleted, or the node came from another graph. Pick another one.";
            }

            valueType = element.GetValueType();
            return null;
        }
    }
}
