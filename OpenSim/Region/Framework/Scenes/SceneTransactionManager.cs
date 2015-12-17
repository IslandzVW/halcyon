/*
 * Copyright (c) 2015, InWorldz Halcyon Developers
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 * 
 *   * Redistributions of source code must retain the above copyright notice, this
 *     list of conditions and the following disclaimer.
 * 
 *   * Redistributions in binary form must reproduce the above copyright notice,
 *     this list of conditions and the following disclaimer in the documentation
 *     and/or other materials provided with the distribution.
 * 
 *   * Neither the name of halcyon nor the names of its
 *     contributors may be used to endorse or promote products derived from
 *     this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using OpenSim.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// Manages transactions to groups of prims.  Prenvents state from being sent to the client until the transaction
    /// is finished, this way the client does not see an inconsistant view of what is going on.
    /// BeginTransaction is safe to call multiple times from the same thread
    /// as it will track the usage counts on a per thread basis using the thread id.
    /// </summary>
    public class SceneTransactionManager
    {
        /// <summary>
        /// This stores the current usage of scene objects by thread id
        /// The structure can be defined as Dictionary[ThreadId, Dictionary[localId, useCount]]
        /// </summary>
        private Dictionary<int, Dictionary<uint, int>> 
            _threadsInTransactions = new Dictionary<int, Dictionary<uint, int>>();

        /// <summary>
        /// The graph we are tracking for
        /// </summary>
        SceneGraph _parent;
        
        
        /// <summary>
        /// Creates a new manager
        /// </summary>
        public SceneTransactionManager(SceneGraph parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Waits on any transaction with the given local id in it
        /// </summary>
        /// <param name="localId"></param>
        public void WaitOnTransaction(uint localId)
        {
            //setting this to true gets us inside the loop
            bool foundIdAlreadyInTransaction = false;
            int currentThreadId = Thread.CurrentThread.ManagedThreadId;

            do
            {
                lock (_threadsInTransactions)
                {
                    foundIdAlreadyInTransaction = false;

                    //look through the thread list for object ids in use
                    foreach (KeyValuePair<int, Dictionary<uint, int>> useCountsByThread in _threadsInTransactions)
                    {
                        //ignore this thread
                        if (useCountsByThread.Key == currentThreadId)
                        {
                            continue;
                        }

                        if (useCountsByThread.Value.ContainsKey(localId))
                        {
                            //we found an id in our list that is already part of a transaction
                            //we have to wait for this to clear, so wait for a pulse and continue
                            Monitor.Wait(_threadsInTransactions);
                            foundIdAlreadyInTransaction = true;
                            break; //break from each thread loop
                        }
                    }
                }

            } while (foundIdAlreadyInTransaction);
        }

        /// <summary>
        /// Begins a transaction with no extra params
        /// </summary>
        /// <param name="objectIds"></param>
        /// <returns></returns>
        public SceneTransaction BeginTransaction(uint id)
        {
            return this.BeginTransaction(new uint[] {id}, null);
        }

        /// <summary>
        /// Begins a transaction with no extra params
        /// </summary>
        /// <param name="objectIds"></param>
        /// <returns></returns>
        public SceneTransaction BeginTransaction(IEnumerable<uint> objectIds)
        {
            return this.BeginTransaction(objectIds, null);
        }

        /// <summary>
        /// Starts a transaction with the given list of object ids and one extra id
        /// </summary>
        /// <param name="objectIds"></param>
        public SceneTransaction BeginTransaction(IEnumerable<uint> objectIds, object extraId)
        {
            //setting this to true gets us inside the loop
            bool foundIdAlreadyInTransaction = true;
            int currentThreadId = Thread.CurrentThread.ManagedThreadId;

            //makes the code easier to read and write by including the extra id in a set
            List<uint> localObjectIds = new List<uint>(objectIds);
            if (extraId != null) localObjectIds.Add((uint)extraId);


            while (foundIdAlreadyInTransaction)
            {
                foundIdAlreadyInTransaction = false;

                lock (_threadsInTransactions)
                {
                    //look through the thread list for object ids in use
                    foreach (KeyValuePair<int, Dictionary<uint, int>> useCountsByThread in _threadsInTransactions)
                    {
                        //ignore this thread
                        if (useCountsByThread.Key == currentThreadId)
                        {
                            continue;
                        }

                        //foreach object in use per thread
                        foreach (uint id in localObjectIds)
                        {
                            if (useCountsByThread.Value.ContainsKey(id))
                            {
                                //we found an id in our list that is already part of a transaction
                                //we have to wait for this to clear, so wait for a pulse and continue
                                Monitor.Wait(_threadsInTransactions);
                                foundIdAlreadyInTransaction = true;
                                break;
                            }
                        }

                        if (foundIdAlreadyInTransaction) break;
                    }

                    //this is good news, we can run our transaction.  we're already holding the lock
                    //so insert our IDs and allow the loop to exit
                    if (!foundIdAlreadyInTransaction)
                    {
                        //we're going to search for the IDs in the list again 
                        this.AddIdsToBusyList(localObjectIds, currentThreadId);
                    }
                }
            }

            return new SceneTransaction(localObjectIds, this);
        }

        /// <summary>
        /// Adds the given IDs to the list of prims already in a transaction
        /// we MUST be already holding a lock
        /// </summary>
        /// <param name="objectIds"></param>
        private void AddIdsToBusyList(IEnumerable<uint> objectIds, int currentThreadId)
        {
            Dictionary<uint, int> threadIds = null;

            //do we have an entry for this thread?
            if (!_threadsInTransactions.ContainsKey(currentThreadId))
            {
                threadIds = new Dictionary<uint,int>();
                _threadsInTransactions.Add(currentThreadId, threadIds);
            }
            else
            {
                threadIds = _threadsInTransactions[currentThreadId];
            }

            //insert each id and/or update it's count
            foreach (uint id in objectIds) 
            {
                if (threadIds.ContainsKey(id))
                {
                    //already has the key, update the value
                    threadIds[id] = threadIds[id] + 1;
                }
                else
                {
                    //insert new object id
                    threadIds.Add(id, 1);
                    this.InformPrimOfTransactionStart(id);
                }
            }
        }

        private void InformPrimOfTransactionStart(uint id)
        {
            SceneObjectPart part = _parent.GetSceneObjectPart(id);
            if (part != null)
            {
                part.IsInTransaction = true;
            }
        }

        private void InformPrimOfTransactionEnd(uint id, bool postUpdates)
        {
            SceneObjectPart part = _parent.GetSceneObjectPart(id);
            if (part != null)
            {
                part.IsInTransaction = false;
                if (!part.ParentGroup.IsDeleted && postUpdates) part.ScheduleFullUpdate(PrimUpdateFlags.ForcedFullUpdate);
            }
        }

        /// <summary>
        /// Removes the given Ids from the transaction list
        /// </summary>
        /// <param name="sceneParts"></param>
        public void EndTransaction(List<uint> sceneParts, SceneTransaction transaction)
        {
            lock (_threadsInTransactions)
            {
                //the thread id MUST be in the collection at this point
                //since the transaction had to have started in order to end
                Dictionary<uint, int> threadIds = _threadsInTransactions[Thread.CurrentThread.ManagedThreadId];

                foreach (uint id in sceneParts)
                {
                    //the id must also exist or there is a bug somewhere
                    int useCount = threadIds[id];
                    if (useCount == 1)
                    {
                        this.InformPrimOfTransactionEnd(id, transaction.PostGroupUpdatesUponCompletion);
                        threadIds.Remove(id);
                    }
                    else
                    {
                        threadIds[id] = useCount - 1;
                    }
                }

                //is our thread holding any more ids?
                if (threadIds.Count == 0)
                {
                    // if not, remove us
                    _threadsInTransactions.Remove(Thread.CurrentThread.ManagedThreadId);
                }

                Monitor.PulseAll(_threadsInTransactions);
            }
        }
    }
}
