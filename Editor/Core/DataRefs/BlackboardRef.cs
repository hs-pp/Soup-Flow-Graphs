using System;
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

        public BlackboardRef(SerializedProperty blackboardSP)
        {
            BlackboardSP = blackboardSP;
            VariablesSP = blackboardSP?.FindPropertyRelative(Blackboard.VariablesFieldName);
        }

        /// <summary>
        /// The blackboard of the graph a SerializedObject was made for. Invalid when its target isn't a graph.
        /// </summary>
        public static BlackboardRef Find(SerializedObject serializedGraph)
        {
            return new BlackboardRef(serializedGraph?.FindProperty(AFlowGraph.BLACKBOARD_FIELD_NAME));
        }

        public bool IsValid => BlackboardSP != null && VariablesSP != null;

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

    public readonly struct BlackboardVariableRef
    {
        public readonly string Id;
        public readonly SerializedProperty VariableSP;
        public readonly int Index;

        public BlackboardVariableRef(SerializedProperty variableSP, int index)
        {
            Id = variableSP?.FindPropertyRelative(ABlackboardVariable.ID_FIELD_NAME)?.stringValue;
            VariableSP = variableSP;
            Index = index;
        }

        public bool IsValid => VariableSP != null;
        public bool IsMissingType => string.IsNullOrEmpty(Id); // Its class was renamed or deleted, so none of its fields resolve.

        public SerializedProperty NameSP => VariableSP?.FindPropertyRelative(ABlackboardVariable.NAME_FIELD_NAME);
        public SerializedProperty ValueSP => VariableSP?.FindPropertyRelative(ABlackboardVariable.VALUE_FIELD_NAME);
        public string Name => NameSP?.stringValue;

        public Type GetVariableType()
        {
            return GetVariableInstance()?.GetType();
        }

        public Type GetValueType()
        {
            return GetVariableInstance()?.ValueType;
        }

        public ABlackboardVariable GetVariableInstance()
        {
            if (!IsValid || VariableSP.propertyType != SerializedPropertyType.ManagedReference)
            {
                return null;
            }

            return VariableSP.managedReferenceValue as ABlackboardVariable;
        }
    }

    public readonly struct BlackboardKeyRef
    {
        public readonly SerializedProperty KeySP;

        public BlackboardKeyRef(SerializedProperty keySP)
        {
            KeySP = keySP;
        }

        public bool IsValid => VariableIdSP != null;

        public SerializedProperty VariableIdSP => KeySP?.FindPropertyRelative(BlackboardKey.VariableIdFieldName);
        public string VariableId => VariableIdSP?.stringValue;
    }
}
