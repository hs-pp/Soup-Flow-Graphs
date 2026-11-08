using System;
using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using Soup.Runtime.Blackboards;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector.Blackboards
{
    /// <summary>
    /// Picks a key's variable from the graph that holds it. A BlackboardKey&lt;T&gt; lists only the variables holding a T. The pick is
    /// written like any binding, so it reaches the canvas as an external edit. Outside a graph there's nothing to pick from, and the
    /// raw id is shown read-only.
    /// </summary>
    [CustomPropertyDrawer(typeof(BlackboardKey), true)]
    public class BlackboardKeyPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            BlackboardKeyRef key = new(property);
            SerializedObject serializedObject = property.serializedObject;
            if (!key.IsValid || !BlackboardRef.Find(serializedObject).IsValid)
            {
                TextField idField = new(property.displayName) { value = key.VariableId, isReadOnly = true };
                idField.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                idField.SetEnabled(false);
                return idField;
            }

            Type valueType = GetKeyValueType(fieldInfo?.FieldType);
            string path = property.propertyPath;
            BlackboardVariablePicker picker = new(property.displayName, type => valueType == null || type == valueType, showTypes: valueType == null);
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

            // Renames, deletes and undo all change what the picker shows without touching this property.
            picker.TrackSerializedObjectValue(serializedObject, _ => RefreshPicker(picker, serializedObject, path));
            RefreshPicker(picker, serializedObject, path);
            return picker;
        }

        private static void RefreshPicker(BlackboardVariablePicker picker, SerializedObject serializedObject, string path)
        {
            picker.Refresh(BlackboardRef.Find(serializedObject), new BlackboardKeyRef(serializedObject.FindProperty(path)).VariableId);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            BlackboardKeyRef key = new(property);
            BlackboardRef blackboard = BlackboardRef.Find(property.serializedObject);
            if (!key.IsValid || !blackboard.IsValid)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.TextField(position, label, key.VariableId);
                }

                EditorGUI.EndProperty();
                return;
            }

            Type valueType = GetKeyValueType(fieldInfo?.FieldType);
            string selectedId = key.VariableId ?? string.Empty;
            List<string> ids = new() { string.Empty };
            List<GUIContent> labels = new() { new GUIContent("None") };
            foreach (BlackboardVariableRef variable in blackboard.GetVariables())
            {
                if (!variable.IsMissingType && (valueType == null || variable.GetValueType() == valueType || variable.Id == selectedId))
                {
                    ids.Add(variable.Id);
                    labels.Add(new GUIContent(variable.Name));
                }
            }

            int index = ids.IndexOf(selectedId);
            if (index < 0)
            {
                ids.Add(selectedId);
                labels.Add(new GUIContent("⚠ Missing variable"));
                index = ids.Count - 1;
            }

            int picked = EditorGUI.Popup(position, label, index, labels.ToArray());
            if (picked != index)
            {
                key.VariableIdSP.stringValue = ids[picked];
            }

            EditorGUI.EndProperty();
        }

        // The T of a BlackboardKey<T> field, or of a list or array of them. Null for an untyped key, which matches any variable.
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

            for (Type t = fieldType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(BlackboardKey<>))
                {
                    return t.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }
}
