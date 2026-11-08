using System;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes
{
    /// <summary>
    /// Very simple logger node. 
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
        
        public const string MessageFieldName = nameof(m_message);
    }
}
