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
        private List<ABlackboardElement> m_elements = new();

        [NonSerialized]
        private Dictionary<string, ABlackboardElement> m_elementCacheById;

        public IReadOnlyList<ABlackboardElement> Elements => m_elements;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        // Any deserialize (a run's copy, an editor Apply, undo) can add, remove or replace elements, so the lookup is rebuilt on next use.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_elementCacheById = null;
        }

        /// <summary>
        /// A deep copy for one run, so the run's writes never reach the asset. FlowGraphController makes one per StartGraph; code
        /// that runs a GraphRunner itself should pass one too, never the asset's own blackboard.
        /// </summary>
        public Blackboard Clone()
        {
            return JsonUtility.FromJson<Blackboard>(JsonUtility.ToJson(this));
        }

        #region Elements
        public ABlackboardElement GetElementById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (m_elementCacheById == null)
            {
                BuildElementCache();
            }

            m_elementCacheById.TryGetValue(id, out ABlackboardElement element);
            return element;
        }

        /// <summary>
        /// Names aren't unique, so this returns the first element with the name.
        /// </summary>
        public ABlackboardElement GetElementByName(string name)
        {
            foreach (ABlackboardElement element in m_elements)
            {
                if (element != null && element.Name == name)
                {
                    return element;
                }
            }

            return null;
        }

        public ABlackboardElement GetElement(BlackboardKey key)
        {
            return key != null ? GetElementById(key.ElementId) : null;
        }

        private void BuildElementCache()
        {
            m_elementCacheById = new Dictionary<string, ABlackboardElement>(m_elements.Count);
            foreach (ABlackboardElement element in m_elements)
            {
                if (element == null || string.IsNullOrEmpty(element.Id))
                {
                    continue;
                }

                if (!m_elementCacheById.TryAdd(element.Id, element))
                {
                    Debug.LogError($"[Soup] Blackboard: duplicate element id '{element.Id}' found.");
                }
            }
        }
        #endregion

        #region Keys
        public T GetValue<T>(BlackboardKey<T> key)
        {
            ABlackboardElement element = GetElement(key);
            if (element is ABlackboardElement<T> typedElement)
            {
                return typedElement.Value;
            }

            LogKeyError(key, element, typeof(T));
            return default;
        }

        public bool TryGetValue<T>(BlackboardKey<T> key, out T value)
        {
            if (GetElement(key) is ABlackboardElement<T> typedElement)
            {
                value = typedElement.Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool SetValue<T>(BlackboardKey<T> key, T value)
        {
            ABlackboardElement element = GetElement(key);
            if (element is ABlackboardElement<T> typedElement)
            {
                typedElement.Value = value;
                return true;
            }

            LogKeyError(key, element, typeof(T));
            return false;
        }

        private static void LogKeyError(BlackboardKey key, ABlackboardElement element, Type valueType)
        {
            if (key == null || !key.IsSet)
            {
                Debug.LogError("[Soup] Blackboard: the key has no variable set.");
            }
            else if (element == null)
            {
                Debug.LogError($"[Soup] Blackboard: no variable has the id '{key.ElementId}'. It was deleted, or the node came from another graph.");
            }
            else
            {
                Debug.LogError($"[Soup] Blackboard: '{element.Name}' holds a {element.ValueType.Name}, not a {valueType.Name}.");
            }
        }
        #endregion

        #region Names
        public bool TryGetValue<T>(string name, out T value)
        {
            if (GetElementByName(name) is ABlackboardElement<T> typedElement)
            {
                value = typedElement.Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool SetValue<T>(string name, T value)
        {
            ABlackboardElement element = GetElementByName(name);
            if (element is ABlackboardElement<T> typedElement)
            {
                typedElement.Value = value;
                return true;
            }

            if (element == null)
            {
                Debug.LogError($"[Soup] Blackboard: no variable is named '{name}'.");
            }
            else
            {
                Debug.LogError($"[Soup] Blackboard: '{name}' holds a {element.ValueType.Name}, not a {typeof(T).Name}.");
            }

            return false;
        }
        #endregion

#if UNITY_EDITOR
        internal const string ElementsFieldName = nameof(m_elements);
#endif
    }
}
