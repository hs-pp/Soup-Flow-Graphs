using System;

namespace Soup.Runtime.Blackboards
{
    [Serializable]
    public sealed class BoolBlackboardVariable : ABlackboardVariable<bool> { }

    [Serializable]
    public sealed class IntBlackboardVariable : ABlackboardVariable<int> { }

    [Serializable]
    public sealed class FloatBlackboardVariable : ABlackboardVariable<float> { }

    [Serializable]
    public sealed class StringBlackboardVariable : ABlackboardVariable<string> { }
}
