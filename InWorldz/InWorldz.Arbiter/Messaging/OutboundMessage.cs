using System.Collections.Generic;
using FlatBuffers;

namespace InWorldz.Arbiter.Messaging
{
    /// <summary>
    /// A message that is on its way to an arbiter
    /// </summary>
    public class OutboundMessage
    {
        private sealed class KeyEqualityComparer : IEqualityComparer<OutboundMessage>
        {
            public bool Equals(OutboundMessage x, OutboundMessage y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.Key.Equals(y.Key);
            }

            public int GetHashCode(OutboundMessage obj)
            {
                return obj.Key.GetHashCode();
            }
        }

        private static readonly IEqualityComparer<OutboundMessage> KeyComparerInstance = new KeyEqualityComparer();

        public static IEqualityComparer<OutboundMessage> KeyComparer
        {
            get { return KeyComparerInstance; }
        }

        public MessageKey Key;
        public FlatBufferBuilder Builder;
    }
}