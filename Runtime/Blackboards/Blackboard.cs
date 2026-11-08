using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soup.Runtime.Blackboards
{
    /// <summary>
    /// A flow graph's variables defined on a per graph instance basis.
    /// </summary>
    [Serializable]
    public sealed class Blackboard : ISerializationCallbackReceiver
    {
        [SerializeReference]
        private List<ABlackboardVariable> m_variables = new();
        public IReadOnlyList<ABlackboardVariable> Variables => m_variables;

        [NonSerialized]
        private Dictionary<string, ABlackboardVariable> m_variableCacheById;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        /// <summary>
        /// Any deserialize (editor apply, undo, reimport, etc) can add, remove or replace variables, so the lookup needs to be rebuilt.
        /// </summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_variableCacheById = null;
        }

        public Blackboard Clone()
        {
            return JsonUtility.FromJson<Blackboard>(JsonUtility.ToJson(this));
        }

        #region Variables
        public ABlackboardVariable GetVariableById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (m_variableCacheById == null)
            {
                BuildVariableCache();
            }

            m_variableCacheById.TryGetValue(id, out ABlackboardVariable variable);
            return variable;
        }

        /// <summary>
        /// Returns the first variable with the name.
        /// </summary>
        public ABlackboardVariable GetVariableByName(string name)
        {
            foreach (ABlackboardVariable variable in m_variables)
            {
                if (variable != null && variable.Name == name)
                {
                    return variable;
                }
            }

            return null;
        }

        public ABlackboardVariable GetVariable(BlackboardKey key)
        {
            return key != null ? GetVariableById(key.VariableId) : null;
        }

        private void BuildVariableCache()
        {
            m_variableCacheById = new Dictionary<string, ABlackboardVariable>(m_variables.Count);
            foreach (ABlackboardVariable variable in m_variables)
            {
                if (variable == null || string.IsNullOrEmpty(variable.Id))
                {
                    continue;
                }

                if (!m_variableCacheById.TryAdd(variable.Id, variable))
                {
                    Debug.LogError($"[Soup] Blackboard: duplicate variable id '{variable.Id}' found.");
                }
            }
        }
        #endregion

        #region Keys
        public T GetValue<T>(BlackboardKey<T> key)
        {
            ABlackboardVariable variable = GetVariable(key);
            if (variable is ABlackboardVariable<T> typedVariable)
            {
                return typedVariable.Value;
            }

            if (key == null || !key.IsSet)
            {
                Debug.LogError("[Soup] Blackboard: the key has no variable set.");
            }
            else if (variable == null)
            {
                Debug.LogError($"[Soup] Blackboard: no variable found with id '{key.VariableId}'.");
            }
            else
            {
                Debug.LogError($"[Soup] Blackboard: '{variable.Name}' is a {variable.ValueType.Name} but expected {typeof(T).Name}.");
            }
            return default;
        }

        public bool TryGetValue<T>(BlackboardKey<T> key, out T value)
        {
            if (GetVariable(key) is ABlackboardVariable<T> typedVariable)
            {
                value = typedVariable.Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool SetValue<T>(BlackboardKey<T> key, T value)
        {
            ABlackboardVariable variable = GetVariable(key);
            if (variable is ABlackboardVariable<T> typedVariable)
            {
                typedVariable.Value = value;
                return true;
            }
            
            return false;
        }
        #endregion

        #region Names
        public bool TryGetValue<T>(string name, out T value)
        {
            if (GetVariableByName(name) is ABlackboardVariable<T> typedVariable)
            {
                value = typedVariable.Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool SetValue<T>(string name, T value)
        {
            ABlackboardVariable variable = GetVariableByName(name);
            if (variable is ABlackboardVariable<T> typedVariable)
            {
                typedVariable.Value = value;
                return true;
            }

            if (variable == null)
            {
                Debug.LogError($"[Soup] Blackboard: no variable is named '{name}'.");
            }
            else
            {
                Debug.LogError($"[Soup] Blackboard: '{name}' holds a {variable.ValueType.Name}, not a {typeof(T).Name}.");
            }

            return false;
        }
        #endregion

        /** For editor use. */
#if UNITY_EDITOR
        internal const string VARIABLES_FIELD_NAME = nameof(m_variables);
#endif
    }
}
