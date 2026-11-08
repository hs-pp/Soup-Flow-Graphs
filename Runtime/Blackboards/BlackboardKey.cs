using System;
using UnityEngine;

namespace Soup.Runtime.Blackboards
{
    /// <summary>
    /// A node's reference to one of its graph's blackboard variables, of any type. Nodes declare the typed one as a field and read and
    /// write the variable through the run's blackboard: <c>[SerializeField] private BlackboardKey&lt;float&gt; m_speed = new();</c>
    /// then <c>context.Blackboard.GetValue(m_speed)</c>.
    /// </summary>
    [Serializable]
    public class BlackboardKey
    {
        [SerializeField]
        private string m_variableId = string.Empty;

        public string VariableId => m_variableId;
        public bool IsSet => !string.IsNullOrEmpty(m_variableId);

#if UNITY_EDITOR
        internal const string VariableIdFieldName = nameof(m_variableId);
#endif
    }

    /// <summary>
    /// A key the editor only lets point at a variable holding a T.
    /// </summary>
    [Serializable]
    public class BlackboardKey<T> : BlackboardKey
    {
    }
}
