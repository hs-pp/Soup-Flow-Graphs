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
    [CustomNode(typeof(AFlowGraph), "Blackboard/Set Variable")]
    public sealed class BlackboardSetterNode : ANode
    {
        [SerializeField]
        private BlackboardKey m_variable = new();

        [SerializeReference]
        private ABlackboardSetter m_setter;

        public override void OnNodeEnter(NodeExecutionContext context)
        {
            ABlackboardVariable variable = context.Blackboard?.GetVariable(m_variable);
            if (variable == null)
            {
                string reason = m_variable.IsSet ? $"no variable has the id '{m_variable.VariableId}'" : "no variable is picked";
                Debug.LogError($"[Soup] BlackboardSetterNode ({Id}): {reason}. Continuing without setting anything.");
            }
            else if (m_setter == null)
            {
                Debug.LogError($"[Soup] BlackboardSetterNode ({Id}): there is no setter for '{variable.Name}'. Continuing without setting anything.");
            }
            else if (!m_setter.TryEvaluate(variable))
            {
                Debug.LogError($"[Soup] BlackboardSetterNode ({Id}): its setter works on {m_setter.ValueType.Name}, but '{variable.Name}' holds a {variable.ValueType.Name}. Continuing without setting anything.");
            }

            context.TraverseEdge(DEFAULT_OUTPORT);
        }

#if UNITY_EDITOR
        internal const string VariableFieldName = nameof(m_variable);
        internal const string SetterFieldName = nameof(m_setter);
#endif
    }
}
