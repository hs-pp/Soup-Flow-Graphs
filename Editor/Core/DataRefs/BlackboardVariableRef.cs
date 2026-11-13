using System;
using Soup.Runtime.Blackboards;
using UnityEditor;

namespace Soup.Editor.Core.DataRefs
{
    public readonly struct BlackboardVariableRef
    {
        public readonly string Id;
        public readonly SerializedProperty VariableSP;
        public readonly int Index;

        public bool IsValid => VariableSP != null;
        public bool IsMissingType => string.IsNullOrEmpty(Id); // Its class was renamed or deleted, so none of its fields resolve.

        public readonly SerializedProperty NameSP;
        public readonly SerializedProperty ValueSP;
        public string Name => NameSP?.stringValue;

        public BlackboardVariableRef(SerializedProperty variableSP, int index)
        {
            Id = variableSP?.FindPropertyRelative(ABlackboardVariable.ID_FIELD_NAME)?.stringValue;
            VariableSP = variableSP;
            Index = index;
            NameSP = VariableSP?.FindPropertyRelative(ABlackboardVariable.NAME_FIELD_NAME);
            ValueSP = VariableSP?.FindPropertyRelative(ABlackboardVariable.VALUE_FIELD_NAME);
        }

        public Type GetVariableType()
        {
            return GetVariableInstance()?.GetType();
        }

        public Type GetValueType()
        {
            return GetVariableInstance()?.ValueType;
        }

        private ABlackboardVariable GetVariableInstance()
        {
            if (!IsValid || VariableSP.propertyType != SerializedPropertyType.ManagedReference)
            {
                return null;
            }

            return VariableSP.managedReferenceValue as ABlackboardVariable;
        }
    }
}
