/**
 * Priority queue (non-blocking, not synchronized)
 * (c) 2010 InWorldz LLC
 */

using System.Collections.Generic;
using System.Threading;

namespace OpenSim.Framework
{
    public class PriorityQueue<T>
    {
        private readonly Queue<T> m_pqueue = new Queue<T>();
        private readonly Queue<T> m_queue = new Queue<T>();

        public void PriorityEnqueue(T value)
        {
            m_pqueue.Enqueue(value);
        }

        public void Enqueue(T value)
        {
            m_queue.Enqueue(value);
        }

        public T Dequeue()
        {
            if (m_pqueue.Count > 0)
                return m_pqueue.Dequeue();

            return m_queue.Dequeue();
        }

        public bool Contains(T item)
        {
            if (m_pqueue.Contains(item))
                return true;

            return m_queue.Contains(item);
        }

        public int Count()
        {
            return m_queue.Count + m_pqueue.Count;
        }

        public T[] GetQueueArray()
        {
            return m_queue.ToArray();
        }

        public void Clear()
        {
            m_pqueue.Clear();
            m_queue.Clear();
        }
    }
}
