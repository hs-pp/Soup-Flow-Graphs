using System;
using Soup.Runtime.Blackboards;

namespace Soup.Runtime.BuiltInNodes.Blackboards
{
    /// <summary>
    /// What a BlackboardSetterNode does to its variable. Setters derive from ABlackboardSetter&lt;T&gt;, never from this directly, and
    /// the editor creates the one whose T matches the picked variable's value type (one setter per value type).
    /// </summary>
    [Serializable]
    public abstract class ABlackboardSetter
    {
        public abstract Type ValueType { get; }

        // Only ABlackboardSetter<T> can call this, so every setter works on exactly one value type.
        private protected ABlackboardSetter()
        {
        }

        /// <summary>
        /// Sets the element's value. False, and nothing changes, when the element holds another type.
        /// </summary>
        public abstract bool TryEvaluate(ABlackboardElement element);

        /// <summary>
        /// What the node shows on the canvas, e.g. "Speed += 1".
        /// </summary>
        public abstract string GetLabel(string elementName);
    }

    [Serializable]
    public abstract class ABlackboardSetter<T> : ABlackboardSetter
    {
        public sealed override Type ValueType => typeof(T);

        public sealed override bool TryEvaluate(ABlackboardElement element)
        {
            if (element is not ABlackboardElement<T> typedElement)
            {
                return false;
            }

            typedElement.Value = Evaluate(typedElement.Value);
            return true;
        }

        /// <summary>
        /// Returns the variable's new value.
        /// </summary>
        protected abstract T Evaluate(T currentValue);
    }
}
