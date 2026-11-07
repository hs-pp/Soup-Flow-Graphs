using System;
using Soup.Runtime.Blackboards;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes.Blackboards
{
    /// <summary>
    /// Changes one blackboard variable, then continues. Picking the variable in the inspector creates the setter for its type. A
    /// node with no variable, no setter or a setter of the wrong type logs and continues without changing anything.
    /// </summary>
    [Serializable]
    [NodeMenu("Blackboard/Set Variable")]
    public sealed class BlackboardSetterNode : ANode
    {
        [SerializeField]
        private BlackboardKey m_element = new();

        [SerializeReference]
        private ABlackboardSetter m_setter;

        public override void OnNodeEnter(NodeExecutionContext context)
        {
            ABlackboardElement element = context.Blackboard?.GetElement(m_element);
            if (element == null)
            {
                string reason = m_element.IsSet ? $"no variable has the id '{m_element.ElementId}'" : "no variable is picked";
                Debug.LogError($"[Soup] BlackboardSetterNode ({Id}): {reason}. Continuing without setting anything.");
            }
            else if (m_setter == null)
            {
                Debug.LogError($"[Soup] BlackboardSetterNode ({Id}): there is no setter for '{element.Name}'. Continuing without setting anything.");
            }
            else if (!m_setter.TryEvaluate(element))
            {
                Debug.LogError($"[Soup] BlackboardSetterNode ({Id}): its setter works on {m_setter.ValueType.Name}, but '{element.Name}' holds a {element.ValueType.Name}. Continuing without setting anything.");
            }

            context.TraverseEdge(DEFAULT_OUTPORT);
        }

#if UNITY_EDITOR
        internal const string ElementFieldName = nameof(m_element);
        internal const string SetterFieldName = nameof(m_setter);
#endif
    }
}
