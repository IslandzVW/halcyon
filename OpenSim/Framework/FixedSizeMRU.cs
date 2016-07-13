using System;
using System.Threading;
using System.Collections.Generic;

namespace OpenSim.Framework
{
    public sealed class FixedSizeMRU<T>
    {
        private Queue<T> mQueue;
        private uint mLimit = 100;
        private double mTotal = 0.0;

        public int Count { get { lock (mQueue) return mQueue.Count; } }

        public FixedSizeMRU(uint limit)
        {
            mLimit = limit;
            mQueue = new Queue<T>();
        }

        public void Clear()
        {
            Init();
        }

        private void Init()
        {
            mQueue = new Queue<T>();
        }

        public T[] ToArray()
        {
            lock (mQueue)
            {
                return mQueue.ToArray();
            }
        }

        private double Value(T item)
        {
            double value = 0.0;
            if (item.GetType() == typeof(System.Double))
                value = (double)Convert.ChangeType(item, typeof(double));
            else
            if (item is int)
                value = (double)(int)Convert.ChangeType(item, typeof(int));
            return value;
        }

        private double[] Values()
        {
            lock (mQueue)
            {
                double[] ret = new double[mQueue.Count];
                int n = 0;
                foreach (T item in mQueue)
                {
                    ret[n++] = Value(item);
                }
                return ret;
            }
        }

        public void Add(T item)
        {
            lock (mQueue)
            {
                while (mQueue.Count >= mLimit)
                {
                    mTotal -= Value(mQueue.Dequeue());
                }
                mQueue.Enqueue(item);
                mTotal += Value(item);
            }
        }

        public double Average()
        {
            lock (mQueue)
            {
                int count = mQueue.Count;
                return (count > 0) ? mTotal / count : 0.0;
            }
        }
    }
}
