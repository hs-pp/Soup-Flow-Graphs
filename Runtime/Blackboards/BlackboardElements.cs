using System;

namespace Soup.Runtime.Blackboards
{
    [Serializable]
    public sealed class BoolBlackboardElement : ABlackboardElement<bool> { }

    [Serializable]
    public sealed class IntBlackboardElement : ABlackboardElement<int> { }

    [Serializable]
    public sealed class FloatBlackboardElement : ABlackboardElement<float> { }

    [Serializable]
    public sealed class StringBlackboardElement : ABlackboardElement<string> { }
}
