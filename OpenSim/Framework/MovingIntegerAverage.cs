/*
 * Copyright(C) 2010 Inworldz LLC
 * Initial Revision:  2010-05-30 David C. Daeschler
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    /// <summary>
    /// Implements a simple moving average
    /// </summary>
    public class MovingIntegerAverage
    {
        private C5.CircularQueue<long> _values;
        private int _sampleSize;

        /// <summary>
        /// Starts a new weighted average with a given maximum sample size
        /// </summary>
        /// <param name="sampleSize"></param>
        public MovingIntegerAverage(int sampleSize)
        {
            _values = new C5.CircularQueue<long>(sampleSize);
            _sampleSize = sampleSize;
        }

        /// <summary>
        /// Adds a new value to this average
        /// </summary>
        /// <param name="value"></param>
        public void AddValue(long value)
        {
            lock (this)
            {
                _values.Push(value);
                if (_values.Count > _sampleSize) _values.Dequeue();
            }
        }

        /// <summary>
        /// Returns the numeric value for this average
        /// </summary>
        /// <returns></returns>
        public int CalculateAverage()
        {
            int total = 0;

            lock (this)
            {
                if (_values.Count == 0) return 0;

                foreach (int value in _values)
                {
                    total += value;
                }

                return total / _values.Count;
            }
        }
    }
}
