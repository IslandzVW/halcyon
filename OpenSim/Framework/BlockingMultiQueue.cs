/**
 * BlockingMultiQueue
 * 
 * Holds more than one queue and waits on any of them to be written to
 * then signals waiters and returns an object.  Gives all queues an even chance of being
 * pulled from
 * 
 * (c) 2010 InWorldz LLC
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpenSim.Framework
{
    public class BlockingMultiQueue<T>
    {
        /// <summary>
        /// Allows us to easily track many different queues
        /// </summary>
        private List<PriorityQueue<T>> _queues = new List<PriorityQueue<T>>();

        /// <summary>
        /// The current queue we're checking for an item.  This will alternate evenly between all queues
        /// to give them an equal chance to be processed
        /// </summary>
        private int _currentQueue = 0;

        public BlockingMultiQueue()
        {

        }

        /// <summary>
        /// Adds a new queue type and returns the identifier to it
        /// </summary>
        public int AddQueueType()
        {
            lock (_queues)
            {
                _queues.Add(new PriorityQueue<T>());
            }

            return _queues.Count - 1;
        }

        public void PriorityEnqueue(int type, T value)
        {
            lock (_queues)
            {
                _queues[type].PriorityEnqueue(value);
                Monitor.Pulse(_queues);
            }
        }

        public void Enqueue(int type, T value)
        {
            lock (_queues)
            {
                _queues[type].Enqueue(value);
                Monitor.Pulse(_queues);
            }
        }

        /// <summary>
        /// Increments the current queue number
        /// NOTE:  MUST BE IN A SYNC BLOCK TO CALL THIS FN
        /// </summary>
        private void SetNextQueueSelection()
        {
            ++_currentQueue;
            if (_currentQueue >= _queues.Count)
            {
                _currentQueue = 0;
            }
        }

        /// <summary>
        /// Finds an item ready to go in the queue
        /// NOTE:  MUST BE IN A SYNC BLOCK TO CALL THIS FN
        /// </summary>
        /// <returns></returns>
        private T FindActiveItem()
        {
            int current = _currentQueue;
            this.SetNextQueueSelection();

            //start on the current queue looking for an item to dequeue
            //then move to each queue.  
            for (int i = 0; i < _queues.Count; ++i)
            {
                int selector = i + current;
                if (selector > (_queues.Count - 1)) 
                {
                    selector -= _queues.Count;
                }

                if (_queues[selector].Count() > 0)
                {
                    return _queues[selector].Dequeue();
                }
            }

            return default(T);
        }

        public T Dequeue()
        {
            lock (_queues)
            {
                T item = this.FindActiveItem();
                 
                //If there are no items, block
                //and wait for one
                if (object.Equals(item, default(T)))
                {
                    Monitor.Wait(_queues);
                }
                else
                {
                    return item;
                }

                //there has to be something now
                return this.FindActiveItem();
            }
        }

        public bool Contains(T item)
        {
            lock (_queues)
            {
                foreach (PriorityQueue<T> queue in _queues)
                {
                    if (queue.Contains(item))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int Count()
        {
            lock (_queues)
            {
                int count = 0;

                foreach (PriorityQueue<T> queue in _queues)
                {
                    count += queue.Count();
                }

                return count;
            }
        }

        public T[] GetQueueArray()
        {
            lock (_queues)
            {
                List<T> combinedList = new List<T>();

                foreach (PriorityQueue<T> queue in _queues)
                {
                    combinedList.AddRange(queue.GetQueueArray());
                }

                return combinedList.ToArray();
            }
        }

        public void Clear()
        {
            lock (_queues)
            {
                foreach (PriorityQueue<T> queue in _queues)
                {
                    queue.Clear();
                }
            }
        }
    }
}
