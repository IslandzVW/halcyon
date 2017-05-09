namespace InWorldz.Arbiter.Messaging
{
    /// <summary>
    /// Defines a key for objects in the message pool based on their 
    /// type and a Mumur2 hash specific to the message type
    /// </summary>
    public struct MessageKey
    {
        public MessageType Type;
        public ulong Hash;
    }
}