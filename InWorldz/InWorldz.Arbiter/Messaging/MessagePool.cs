using System.Collections;
using System.Collections.Generic;
using C5;
using OpenMetaverse;

namespace InWorldz.Arbiter.Messaging
{
    /// <summary>
    /// Stores messages that need to be fanned out to clients of an arbiter process
    /// </summary>
    public class MessagePool
    {
        private byte[] _simId;
        private Queue<OutboundMessage> _queue = new Queue<OutboundMessage>();
        private C5.HashedLinkedList<OutboundMessage> _messages = new HashedLinkedList<OutboundMessage>(OutboundMessage.KeyComparer);

        /// <summary>
        /// Ctor for a new message pool
        /// </summary>
        /// <param name="simId">The simulator ID this pool is servicing</param>
        public MessagePool(UUID simId)
        {
            _simId = simId.GetBytes();
        }

        /// <summary>
        /// Queues a prim full update
        /// </summary>
        void QueuePrimUpdate()
        {
            lock (_queue)
            {
                //FindOrGetOutboundMessage(new MessageKey() { })
            }
        }

    }
}