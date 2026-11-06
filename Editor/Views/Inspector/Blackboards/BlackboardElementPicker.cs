using System;
using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector.Blackboards
{
    /// <summary>
    /// A popup of the graph's blackboard elements. It always shows what the model holds: a pick is only handed to OnPicked, and the
    /// owner writes it (a binding or a command) and calls Refresh.
    /// </summary>
    public class BlackboardElementPicker : VisualElement
    {
        private const string NONE_ID = "";
        private const string NONE_LABEL = "None";
        private const string WARNING_PREFIX = "⚠ ";
        private const string MISSING_LABEL = WARNING_PREFIX + "Missing variable";

        private readonly PopupField<string> m_popup;
        private readonly Func<Type, bool> m_filter;
        private readonly bool m_showTypes;
        private readonly Dictionary<string, string> m_labelsById = new();

        public event Action<string> OnPicked;

        // The filter takes an element's value type and decides whether it is listed. Types are shown when the filter allows several.
        public BlackboardElementPicker(string label, Func<Type, bool> filter = null, bool showTypes = false)
        {
            m_filter = filter;
            m_showTypes = showTypes;

            m_popup = new PopupField<string>(label, new List<string> { NONE_ID }, 0, FormatChoice, FormatChoice);
            m_popup.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            m_popup.RegisterValueChangedCallback(HandleValueChanged);
            Add(m_popup);
        }

        public void Refresh(BlackboardRef blackboard, string selectedId)
        {
            selectedId ??= NONE_ID;

            m_labelsById.Clear();
            m_labelsById[NONE_ID] = NONE_LABEL;
            List<string> choices = new() { NONE_ID };
            foreach (BlackboardElementRef element in blackboard.GetElements())
            {
                if (element.IsMissingType || m_labelsById.ContainsKey(element.Id))
                {
                    continue;
                }

                Type valueType = element.GetValueType();
                bool isRejected = m_filter != null && !m_filter(valueType);
                if (isRejected && element.Id != selectedId)
                {
                    continue;
                }

                // A pick the filter rejects, like a typed key whose T changed since, stays listed but marked, with its type.
                string typeName = EditorTypesFactory.GetBlackboardValueTypeDisplayName(valueType);
                choices.Add(element.Id);
                m_labelsById[element.Id] = isRejected ? $"{WARNING_PREFIX}{element.Name} ({typeName})"
                    : m_showTypes ? $"{element.Name} ({typeName})"
                    : element.Name;
            }

            // A deleted pick stays listed so the field can show it as missing.
            if (!m_labelsById.ContainsKey(selectedId))
            {
                choices.Add(selectedId);
            }

            m_popup.choices = choices;
            m_popup.SetValueWithoutNotify(selectedId);
        }

        private string FormatChoice(string id)
        {
            return m_labelsById.TryGetValue(id ?? NONE_ID, out string label) ? label : MISSING_LABEL;
        }

        private void HandleValueChanged(ChangeEvent<string> evt)
        {
            evt.StopPropagation();
            m_popup.SetValueWithoutNotify(evt.previousValue);
            if (evt.newValue != evt.previousValue)
            {
                OnPicked?.Invoke(evt.newValue ?? NONE_ID);
            }
        }
    }
}
