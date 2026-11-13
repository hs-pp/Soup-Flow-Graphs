using System.Collections.Generic;
using Soup.Runtime;
using Soup.Runtime.Blackboards;
using UnityEditor;

namespace Soup.Editor.Core.DataRefs
{
    public readonly struct BlackboardRef
    {
        public readonly SerializedProperty BlackboardSP;
        public readonly SerializedProperty VariablesSP;

        public bool IsValid => BlackboardSP != null && VariablesSP != null;

        public BlackboardRef(SerializedObject serializedGraph)
        {
            BlackboardSP = serializedGraph?.FindProperty(AFlowGraph.BLACKBOARD_FIELD_NAME);
            VariablesSP = BlackboardSP?.FindPropertyRelative(Blackboard.VARIABLES_FIELD_NAME);
        }

        public IEnumerable<BlackboardVariableRef> GetVariables()
        {
            if (!IsValid)
            {
                yield break;
            }

            for (int i = 0; i < VariablesSP.arraySize; i++)
            {
                yield return new BlackboardVariableRef(VariablesSP.GetArrayElementAtIndex(i), i);
            }
        }

        public bool TryGetVariable(string variableId, out BlackboardVariableRef variable)
        {
            variable = default;
            if (!IsValid || string.IsNullOrEmpty(variableId))
            {
                return false;
            }

            foreach (BlackboardVariableRef candidate in GetVariables())
            {
                if (candidate.Id == variableId)
                {
                    variable = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
