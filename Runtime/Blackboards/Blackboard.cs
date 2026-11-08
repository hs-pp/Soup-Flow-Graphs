using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soup.Runtime.Blackboards
{
    /// <summary>
    /// A graph asset's own variables, added and edited in the graph editor. Every run gets its own copy, so the asset's values are
    /// only defaults. Nodes reach the run's copy through NodeExecutionContext.Blackboard and address variables with BlackboardKey
    /// fields; code outside the graph goes through FlowGraphController.GraphRunner.Blackboard and addresses them by name. Data every graph of a
    /// type needs belongs in its GraphProperties instead: nothing guarantees a variable exists in a particular asset.
    /// </summary>
    [Serializable]
    public sealed class Blackboard : ISerializationCallbackReceiver
    {
        [SerializeReference]
        private List<ABlackboardVariable> m_variables = new();

        [NonSerialized]
        private Dictionary<string, ABlackboardVariable> m_variableCacheById;

        public IReadOnlyList<ABlackboardVariable> Variables => m_variables;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        // Any deserialize (a run's copy, an editor Apply, undo) can add, remove or replace variables, so the lookup is rebuilt on next use.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_variableCacheById = null;
        }

        /// <summary>
        /// A deep copy for one run, so the run's writes never reach the asset. FlowGraphController makes one per StartGraph; code
        /// that runs a GraphRunner itself should pass one too, never the asset's own blackboard.
        /// </summary>
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
        /// Names aren't unique, so this returns the first variable with the name.
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

            LogKeyError(key, variable, typeof(T));
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

            LogKeyError(key, variable, typeof(T));
            return false;
        }

        private static void LogKeyError(BlackboardKey key, ABlackboardVariable variable, Type valueType)
        {
            if (key == null || !key.IsSet)
            {
                Debug.LogError("[Soup] Blackboard: the key has no variable set.");
            }
            else if (variable == null)
            {
                Debug.LogError($"[Soup] Blackboard: no variable has the id '{key.VariableId}'. It was deleted, or the node came from another graph.");
            }
            else
            {
                Debug.LogError($"[Soup] Blackboard: '{variable.Name}' holds a {variable.ValueType.Name}, not a {valueType.Name}.");
            }
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

#if UNITY_EDITOR
        internal const string VariablesFieldName = nameof(m_variables);
#endif
    }
}
