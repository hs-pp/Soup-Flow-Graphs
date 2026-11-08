using System;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes
{
    /// <summary>
    /// Very simple logger node. Logs its message to the Unity console when it enters, then leaves.
    /// </summary>
    [Serializable]
    [CustomNode(typeof(AFlowGraph), "Debug/Log Message")]
    public sealed class LogNode : ANode
    {
        [SerializeField]
        private string m_message = "Hello from LogNode!";

        public override void OnNodeEnter(NodeExecutionContext context)
        {
            Debug.Log(m_message);
            context.TraverseEdge(DEFAULT_OUTPORT);
        }
        
        /** For editor use. */
#if UNITY_EDITOR
        internal const string MESSAGE_FIELD_NAME = nameof(m_message);
#endif
    }
}
