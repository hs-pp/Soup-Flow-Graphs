using System;
using System.Collections.Generic;
using Soup.Editor.Core.DataRefs;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Inspector.Blackboards
{
    public class BlackboardVariablePicker : VisualElement
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

        // The filter takes a variable's value type and decides whether it is listed. Types are shown when the filter allows several.
        public BlackboardVariablePicker(string label, Func<Type, bool> filter = null, bool showTypes = false)
        {
            m_filter = filter;
            m_showTypes = showTypes;

            // The popup formats its first value while it's built, before any Refresh.
            m_labelsById[NONE_ID] = NONE_LABEL;
            m_popup = new PopupField<string>(label, new List<string> { NONE_ID }, 0, FormatChoice, FormatChoice);
            m_popup.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            m_popup.RegisterValueChangedCallback(HandleValueChanged);
            Add(m_popup);
        }

        public void Refresh(BlackboardRef blackboard, string selectedId)
        {
            if (selectedId == null)
            {
                selectedId = NONE_ID;
            }

            m_labelsById.Clear();
            m_labelsById[NONE_ID] = NONE_LABEL;
            List<string> choices = new() { NONE_ID };
            foreach (BlackboardVariableRef variable in blackboard.GetVariables())
            {
                if (variable.IsMissingType || m_labelsById.ContainsKey(variable.Id))
                {
                    continue;
                }

                Type valueType = variable.GetValueType();
                bool isRejected = m_filter != null && !m_filter(valueType);
                if (isRejected && variable.Id != selectedId)
                {
                    continue;
                }

                // A pick the filter rejects, like a typed key whose T changed since, stays listed but marked, with its type.
                string label = variable.Name;
                if (isRejected || m_showTypes)
                {
                    label = $"{label} ({EditorTypesFactory.GetBlackboardValueTypeDisplayName(valueType)})";
                }

                if (isRejected)
                {
                    label = WARNING_PREFIX + label;
                }

                choices.Add(variable.Id);
                m_labelsById[variable.Id] = label;
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
            return m_labelsById.TryGetValue(id, out string label) ? label : MISSING_LABEL;
        }

        private void HandleValueChanged(ChangeEvent<string> evt)
        {
            evt.StopPropagation();
            m_popup.SetValueWithoutNotify(evt.previousValue);
            if (evt.newValue != evt.previousValue)
            {
                OnPicked?.Invoke(evt.newValue);
            }
        }
    }
}
