using System;
using Soup.Runtime.Blackboards;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes.Blackboards
{
    /// <summary>
    /// Changes one blackboard variable.
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
                if (m_variable.IsSet)
                {
                    Debug.LogError($"[Soup] BlackboardSetterNode ({Id}): no variable has the id '{m_variable.VariableId}'. Skipping.");
                }
                else
                {
                    Debug.LogError($"[Soup] BlackboardSetterNode ({Id}): no variable is set. Skipping.");
                }
            }
            else if (m_setter == null)
            {
                Debug.LogError($"[Soup] BlackboardSetterNode ({Id}): there is no setter for '{variable.Name}'. Skipping.");
            }
            else if (!m_setter.TryApply(variable))
            {
                Debug.LogError($"[Soup] BlackboardSetterNode ({Id}): its setter works on {m_setter.ValueType.Name}, but '{variable.Name}' holds a {variable.ValueType.Name}. Skipping.");
            }

            context.TraverseEdge(DEFAULT_OUTPORT);
        }

        /** For editor use. */
#if UNITY_EDITOR
        internal const string VARIABLE_FIELD_NAME = nameof(m_variable);
        internal const string SETTER_FIELD_NAME = nameof(m_setter);
#endif
    }
}
