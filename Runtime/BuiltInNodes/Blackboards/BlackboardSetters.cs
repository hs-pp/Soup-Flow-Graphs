using System;
using System.Globalization;
using UnityEngine;

namespace Soup.Runtime.BuiltInNodes.Blackboards
{
    [Serializable]
    public sealed class BoolBlackboardSetter : ABlackboardSetter<bool>
    {
        public enum Operation
        {
            SetTo,
            Toggle,
        }

        [SerializeField]
        private Operation m_operation = Operation.SetTo;

        [SerializeField]
        private bool m_value;

        protected override bool Evaluate(bool currentValue)
        {
            return m_operation == Operation.Toggle ? !currentValue : m_value;
        }

        public override string GetLabel(string variableName)
        {
            return m_operation == Operation.Toggle ? $"Toggle {variableName}" : $"{variableName} = {(m_value ? "true" : "false")}";
        }
    }

    [Serializable]
    public sealed class IntBlackboardSetter : ABlackboardSetter<int>
    {
        public enum Operation
        {
            SetTo,
            Increment,
            Decrement,
        }

        [SerializeField]
        private Operation m_operation = Operation.SetTo;

        [SerializeField]
        private int m_value;

        protected override int Evaluate(int currentValue)
        {
            switch (m_operation)
            {
                case Operation.Increment:
                    return currentValue + m_value;
                case Operation.Decrement:
                    return currentValue - m_value;
                default:
                    return m_value;
            }
        }

        public override string GetLabel(string variableName)
        {
            string symbol = m_operation switch
            {
                Operation.Increment => "+=",
                Operation.Decrement => "-=",
                _ => "=",
            };
            return $"{variableName} {symbol} {m_value.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    [Serializable]
    public sealed class FloatBlackboardSetter : ABlackboardSetter<float>
    {
        public enum Operation
        {
            SetTo,
            Increment,
            Decrement,
        }

        [SerializeField]
        private Operation m_operation = Operation.SetTo;

        [SerializeField]
        private float m_value;

        protected override float Evaluate(float currentValue)
        {
            switch (m_operation)
            {
                case Operation.Increment:
                    return currentValue + m_value;
                case Operation.Decrement:
                    return currentValue - m_value;
                default:
                    return m_value;
            }
        }

        public override string GetLabel(string variableName)
        {
            string symbol = m_operation switch
            {
                Operation.Increment => "+=",
                Operation.Decrement => "-=",
                _ => "=",
            };
            return $"{variableName} {symbol} {m_value.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    [Serializable]
    public sealed class StringBlackboardSetter : ABlackboardSetter<string>
    {
        [SerializeField]
        private string m_value = string.Empty;

        protected override string Evaluate(string currentValue)
        {
            return m_value;
        }

        public override string GetLabel(string variableName)
        {
            return $"{variableName} = \"{m_value}\"";
        }
    }
}
