using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes
{
    /// <summary>
    /// Runs its steps one after another.
    /// </summary>
    [Serializable]
    [CustomNode(typeof(AFlowGraph), "Flow/Sequence")]
    public sealed class SequenceNode : ANode
    {
        public const string COMPLETED_OUTPORT = "On Completed";

        [SerializeField]
        private int m_stepCount = 2;

        public override void DefineOutports(List<OutportDefinition> outports)
        {
            for (int i = 0; i < m_stepCount; i++)
            {
                string outportId = GetStepOutportId(i);
                outports.Add(new OutportDefinition(outportId, outportId));
            }

            outports.Add(new OutportDefinition(COMPLETED_OUTPORT, COMPLETED_OUTPORT));
        }

        public override void OnNodeEnter(NodeExecutionContext context)
        {
            if (m_stepCount <= 0)
            {
                context.TraverseEdge(COMPLETED_OUTPORT);
                return;
            }

            context.PushEdgeToReturnStack(COMPLETED_OUTPORT);
            for (int i = m_stepCount - 1; i >= 1; i--)
            {
                context.PushEdgeToReturnStack(GetStepOutportId(i));
            }

            context.TraverseEdge(GetStepOutportId(0));
        }

        /// <summary>
        /// Steps count from 0, and their outports from "Then 1".
        /// </summary>
        private static string GetStepOutportId(int step)
        {
            return "Then " + (step + 1);
        }
    }
}
