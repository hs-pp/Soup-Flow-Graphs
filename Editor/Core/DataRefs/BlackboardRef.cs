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
        public readonly SerializedProperty ElementsSP;

        public BlackboardRef(SerializedProperty blackboardSP)
        {
            BlackboardSP = blackboardSP;
            ElementsSP = blackboardSP?.FindPropertyRelative(Blackboard.ElementsFieldName);
        }

        /// <summary>
        /// The blackboard of the graph a SerializedObject was made for. Invalid when its target isn't a graph.
        /// </summary>
        public static BlackboardRef Find(SerializedObject serializedGraph)
        {
            return new BlackboardRef(serializedGraph?.FindProperty(AFlowGraph.BLACKBOARD_FIELD_NAME));
        }

        public bool IsValid => BlackboardSP != null && ElementsSP != null;

        public IEnumerable<BlackboardElementRef> GetElements()
        {
            if (!IsValid)
            {
                yield break;
            }

            for (int i = 0; i < ElementsSP.arraySize; i++)
            {
                yield return new BlackboardElementRef(ElementsSP.GetArrayElementAtIndex(i), i);
            }
        }

        public bool TryGetElement(string elementId, out BlackboardElementRef element)
        {
            element = default;
            if (!IsValid || string.IsNullOrEmpty(elementId))
            {
                return false;
            }

            foreach (BlackboardElementRef candidate in GetElements())
            {
                if (candidate.Id == elementId)
                {
                    element = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    public readonly struct BlackboardElementRef
    {
        public readonly string Id;
        public readonly SerializedProperty ElementSP;
        public readonly int Index;

        public BlackboardElementRef(SerializedProperty elementSP, int index)
        {
            Id = elementSP?.FindPropertyRelative(ABlackboardElement.IdFieldName)?.stringValue;
            ElementSP = elementSP;
            Index = index;
        }

        public bool IsValid => ElementSP != null;
        public bool IsMissingType => string.IsNullOrEmpty(Id); // Its class was renamed or deleted, so none of its fields resolve.

        public SerializedProperty NameSP => ElementSP?.FindPropertyRelative(ABlackboardElement.NameFieldName);
        public SerializedProperty ValueSP => ElementSP?.FindPropertyRelative(ABlackboardElement.ValueFieldName);
        public string Name => NameSP?.stringValue;

        public Type GetElementType()
        {
            return GetElementInstance()?.GetType();
        }

        public Type GetValueType()
        {
            return GetElementInstance()?.ValueType;
        }

        public ABlackboardElement GetElementInstance()
        {
            if (!IsValid || ElementSP.propertyType != SerializedPropertyType.ManagedReference)
            {
                return null;
            }

            return ElementSP.managedReferenceValue as ABlackboardElement;
        }
    }

    public readonly struct BlackboardKeyRef
    {
        public readonly SerializedProperty KeySP;

        public BlackboardKeyRef(SerializedProperty keySP)
        {
            KeySP = keySP;
        }

        public bool IsValid => ElementIdSP != null;

        public SerializedProperty ElementIdSP => KeySP?.FindPropertyRelative(BlackboardKey.ElementIdFieldName);
        public string ElementId => ElementIdSP?.stringValue;
    }
}
