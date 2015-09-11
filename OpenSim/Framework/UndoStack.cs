/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;

namespace OpenSim.Framework
{
    /// <summary>
    /// Undo stack.  Deletes entries beyond a certain capacity
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class UndoStack<T>
    {
        private Stack<T> _undos;
        private int _capacity;

        public UndoStack(int capacity)
        {
            _capacity = capacity;
            _undos = new Stack<T>(capacity);
        }

        public bool IsFull
        {
            get { return _undos.Count == Capacity; }
        }

        public int Capacity
        {
            get { return _capacity; }
        }

        public int Count
        {
            get
            {
                return _undos.Count;
            }
        }

        public void Push(T item)
        {
            if (_undos.Count + 1 > Capacity)
            {
                //Compress the stack
                List<T> ts = new List<T>(_undos.ToArray());
                ts.RemoveAt(Capacity - 1);
                ts.Reverse();
                _undos = new Stack<T>(ts);
            }
            _undos.Push(item);
        }

        public T Pop()
        {
            if (Count > 0)
            {
                return _undos.Pop();
            }
            else
                throw new InvalidOperationException("Cannot pop from empty stack");
        }

        public T Peek()
        {
            return _undos.Peek();
        }

        public void Clear()
        {
            _undos.Clear();
        }
    }
}
