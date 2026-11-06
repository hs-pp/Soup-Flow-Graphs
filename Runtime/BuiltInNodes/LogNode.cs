using System;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes
{
    /// <summary>
    /// Logs a message when entered, then continues on the next tick. Mostly
    /// here as the simplest possible authorable node for testing graphs.
    /// </summary>
    [Serializable]
    [Outports(OutPort)]
    [NodeMenu("Debug/Log Message")]
    public sealed class LogNode : ANode
    {
        public const string OutPort = "Out";

        [SerializeField]
        private string m_message = "Hello from LogNode!";

        public override void OnNodeEnter(NodeExecutionContext context)
        {
            Debug.Log(m_message);
            context.TraverseEdge(OutPort);
        }
        
        public const string MessageFieldName = nameof(m_message);
    }
}
