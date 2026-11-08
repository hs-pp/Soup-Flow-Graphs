using System;
using Soup.Editor.Core.DataRefs;

namespace Soup.Editor.Views.Inspector.Blackboards
{
    internal static class BlackboardInspectorUtils
    {
        public static string GetVariableId(NodeRef nodeRef, string keyFieldName)
        {
            return new BlackboardKeyRef(nodeRef.NodeSP?.FindPropertyRelative(keyFieldName)).VariableId;
        }

        /// <summary>
        /// Why a node's variable can't be used, or null when it can. Missing strategy types are reported by the caller.
        /// </summary>
        public static string GetVariableProblem(BlackboardRef blackboard, string variableId, out BlackboardVariableRef variable, out Type valueType)
        {
            variable = default;
            valueType = null;
            if (string.IsNullOrEmpty(variableId))
            {
                return "Pick a variable.";
            }

            if (!blackboard.TryGetVariable(variableId, out variable))
            {
                return "The variable was deleted, or the node came from another graph. Pick another one.";
            }

            valueType = variable.GetValueType();
            return null;
        }
    }
}
