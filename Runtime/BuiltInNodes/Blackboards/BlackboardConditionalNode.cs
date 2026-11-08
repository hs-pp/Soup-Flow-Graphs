using System;
using System.Collections.Generic;
using Soup.Runtime.Blackboards;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes.Blackboards
{
    /// <summary>
    /// Tests one blackboard variable against a list of conditions.
    /// </summary>
    [Serializable]
    [CustomNode(typeof(AFlowGraph), "Blackboard/Conditional")]
    public sealed class BlackboardConditionalNode : ANode
    {
        public const string ELSE_OUTPORT = "Else";

        [SerializeField]
        private BlackboardKey m_variable = new();

        [SerializeField]
        private List<BlackboardConditionalEntry> m_entries = new();

        public override void DefineOutports(List<OutportDefinition> outports)
        {
            foreach (BlackboardConditionalEntry entry in m_entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.OutportId))
                {
                    string label = "(no condition)";
                    if (entry.Conditional != null)
                    {
                        label = entry.Conditional.GetLabel();
                    }

                    outports.Add(new OutportDefinition(entry.OutportId, label));
                }
            }

            outports.Add(new OutportDefinition(ELSE_OUTPORT, ELSE_OUTPORT));
        }

        public override void OnNodeEnter(NodeExecutionContext context)
        {
            ABlackboardVariable variable = context.Blackboard?.GetVariable(m_variable);
            if (variable == null)
            {
                if (m_variable.IsSet)
                {
                    Debug.LogError($"[Soup] BlackboardConditionalNode ({Id}): no variable has the id '{m_variable.VariableId}'. Taking Else.");
                }
                else
                {
                    Debug.LogError($"[Soup] BlackboardConditionalNode ({Id}): no variable is set. Taking Else.");
                }
                
                context.TraverseEdge(ELSE_OUTPORT);
                return;
            }

            for (int i = 0; i < m_entries.Count; i++)
            {
                BlackboardConditionalEntry entry = m_entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.OutportId))
                {
                    Debug.LogError($"[Soup] BlackboardConditionalNode ({Id}): condition {i + 1} has no outport, so it is skipped.");
                    continue;
                }

                ABlackboardConditional conditional = entry.Conditional;
                if (conditional == null)
                {
                    Debug.LogError($"[Soup] BlackboardConditionalNode ({Id}): condition {i + 1} is empty, so it is skipped.");
                    continue;
                }

                if (!conditional.TryEvaluate(variable, out bool isTrue))
                {
                    Debug.LogError($"[Soup] BlackboardConditionalNode ({Id}): condition {i + 1} works on {conditional.ValueType.Name}, but '{variable.Name}' holds a {variable.ValueType.Name}. Taking Else.");
                    context.TraverseEdge(ELSE_OUTPORT);
                    return;
                }

                if (isTrue)
                {
                    context.TraverseEdge(entry.OutportId);
                    return;
                }
            }

            context.TraverseEdge(ELSE_OUTPORT);
        }

        /** For editor use. */
#if UNITY_EDITOR
        internal const string VARIABLE_FIELD_NAME = nameof(m_variable);
        internal const string ENTRIES_FIELD_NAME = nameof(m_entries);
#endif
    }

    /// <summary>
    /// One condition and the id of the outport it leads to.
    /// </summary>
    [Serializable]
    public sealed class BlackboardConditionalEntry
    {
        [SerializeField, HideInInspector]
        private string m_outportId;
        public string OutportId => m_outportId;
        
        [SerializeReference]
        private ABlackboardConditional m_conditional;
        public ABlackboardConditional Conditional => m_conditional;

        /** For editor use. */
#if UNITY_EDITOR
        internal const string OUTPORT_ID_FIELD_NAME = nameof(m_outportId);
        internal const string CONDITIONAL_FIELD_NAME = nameof(m_conditional);
#endif
    }
}
