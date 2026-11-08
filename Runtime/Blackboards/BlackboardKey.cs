using System;
using UnityEngine;

namespace Soup.Runtime.Blackboards
{
    /// <summary>
    /// A node's reference to one of its graph's blackboard variables.
    /// </summary>
    [Serializable]
    public class BlackboardKey
    {
        [SerializeField]
        private string m_variableId = string.Empty;
        public string VariableId => m_variableId;
        
        public bool IsSet => !string.IsNullOrEmpty(m_variableId);

        /** For editor use. */
#if UNITY_EDITOR
        internal const string VARIABLE_ID_FIELD_NAME = nameof(m_variableId);
#endif
    }

    /// <summary>
    /// A key the editor only lets point at a variable holding a T. Doesn't do much here but very important on the editor side.
    /// </summary>
    [Serializable]
    public sealed class BlackboardKey<T> : BlackboardKey { }
}
