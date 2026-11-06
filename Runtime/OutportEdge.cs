namespace Soup.Runtime
{
    /// <summary>
    /// This class represents an outgoing port on a node.
    /// If the ConnectedNodeId is null, we can infer that the outport exists but a connection out of it does not.
    /// 
    /// These node connections are singly linked. InportEdge as a class does not exist.
    /// </summary>
    [System.Serializable]
    public class OutportEdge
    {
        public string Id;
        public string ConnectedNodeId;
    }
}