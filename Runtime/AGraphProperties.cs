using System;

namespace Soup.Runtime
{
    [Serializable]
    public abstract class AGraphProperties { }

    [Serializable]
    public class EmptyGraphProperties : AGraphProperties
    {
        public int a;
        public string v;
    }
}
