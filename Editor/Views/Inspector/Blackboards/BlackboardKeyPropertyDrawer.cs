using System;
using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.Blackboards;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector.Blackboards
{
    [CustomPropertyDrawer(typeof(BlackboardKey), true)]
    public class BlackboardKeyPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            BlackboardKeyRef key = new(property);
            SerializedObject serializedObject = property.serializedObject;
            if (!key.IsValid || !new BlackboardRef(serializedObject).IsValid)
            {
                TextField idField = new(preferredLabel) { isReadOnly = true };
                if (key.IsValid)
                {
                    idField.BindProperty(key.VariableIdSP);
                }

                idField.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                idField.SetEnabled(false);
                return idField;
            }

            Type valueType = GetKeyValueType(fieldInfo?.FieldType);
            string path = property.propertyPath;
            BlackboardVariablePicker picker = new(preferredLabel, type => valueType == null || type == valueType, showTypes: valueType == null);
            picker.OnPicked += variableId =>
            {
                SerializedProperty idSP = new BlackboardKeyRef(serializedObject.FindProperty(path)).VariableIdSP;
                if (idSP != null)
                {
                    idSP.stringValue = variableId;
                    serializedObject.ApplyModifiedProperties();
                }

                RefreshPicker(picker, serializedObject, path);
            };

            picker.TrackSerializedObjectValue(serializedObject, _ => RefreshPicker(picker, serializedObject, path));
            RefreshPicker(picker, serializedObject, path);
            return picker;
        }

        private static void RefreshPicker(BlackboardVariablePicker picker, SerializedObject serializedObject, string path)
        {
            picker.Refresh(new BlackboardRef(serializedObject), new BlackboardKeyRef(serializedObject.FindProperty(path)).VariableId);
        }

        private static Type GetKeyValueType(Type fieldType)
        {
            if (fieldType == null)
            {
                return null;
            }

            if (fieldType.IsArray)
            {
                fieldType = fieldType.GetElementType();
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                fieldType = fieldType.GetGenericArguments()[0];
            }

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(BlackboardKey<>))
            {
                return fieldType.GetGenericArguments()[0];
            }

            return null;
        }
    }
}
