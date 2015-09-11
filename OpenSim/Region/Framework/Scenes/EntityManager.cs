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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Scenes
{
    public class EntityManager : IEnumerable<EntityBase>
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Dictionary<UUID, EntityBase> m_eb_uuid = new Dictionary<UUID, EntityBase>();
        private readonly Dictionary<uint, EntityBase> m_eb_localID = new Dictionary<uint, EntityBase>();
        //private readonly Dictionary<UUID, ScenePresence> m_pres_uuid = new Dictionary<UUID, ScenePresence>();
        private readonly Object m_lock = new Object();

        [Obsolete("Use Add() instead.")]
        public void Add(UUID id, EntityBase eb)
        {
            Add(eb);
        }

        private void PrepUpdate(EntityBase entity)
        {
            try
            {
                // This can be simplified a lot for normal production use but the extra debugging here is 
                // to help identify if, and which, error cases may be occurring in practice.
                EntityBase oldEntity = null;
                if (m_eb_uuid.ContainsKey(entity.UUID))
                {
                    oldEntity = m_eb_uuid[entity.UUID];
                    if (Object.ReferenceEquals(entity, oldEntity))
                        m_log.WarnFormat("Add Entity: redundant add for UUID {0} {1}", entity.UUID.ToString(), entity.LocalId.ToString());
                    else // Not the same, even if it has the same values.
                    if (m_eb_localID.ContainsKey(entity.LocalId))
                        m_log.ErrorFormat("Add Entity: duplicate add for UUID {0} {1}", entity.UUID.ToString(), entity.LocalId.ToString());
                    else
                        m_log.ErrorFormat("Add Entity: update for UUID {0} to {1} from {2}", entity.UUID.ToString(), entity.LocalId.ToString(), oldEntity.LocalId.ToString());
                }
                else
                if (m_eb_uuid.ContainsKey(entity.UUID))
                {
                    oldEntity = m_eb_localID[entity.LocalId];
                    if (Object.ReferenceEquals(entity, oldEntity))
                        m_log.WarnFormat("Add Entity: redundant add for LocalId {0} {1}", entity.LocalId.ToString(), entity.UUID.ToString());
                    else // Not the same, even if it has the same values.
                    if (m_eb_uuid.ContainsKey(entity.UUID))
                        m_log.ErrorFormat("Add Entity: duplicate add for LocalId {0} {1}", entity.LocalId.ToString(), entity.UUID.ToString());
                    else
                        m_log.ErrorFormat("Add Entity: update for LocalId {0} to {1} from {2}", entity.LocalId.ToString(), entity.UUID.ToString(), oldEntity.UUID.ToString());
                }
                if (oldEntity != null)
                {
                    // We have at least a partial conflict, remove any old entries.
                    if (m_eb_localID.ContainsKey(oldEntity.LocalId))
                        m_eb_localID.Remove(oldEntity.LocalId);
                    if (m_eb_uuid.ContainsKey(oldEntity.UUID))
                        m_eb_uuid.Remove(oldEntity.UUID);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("Add Entity failed: {0}", e.Message);
            }
        }

        public void Add(EntityBase entity)
        {
            lock (m_lock)
            {
                try
                {
                    PrepUpdate(entity);
                    m_eb_uuid.Add(entity.UUID, entity);
                    m_eb_localID.Add(entity.LocalId, entity);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("Add Entity failed: {0}", e.Message);
                }
            }
        }

        public void Clear()
        {
            lock (m_lock)
            {
                m_eb_uuid.Clear();
                m_eb_localID.Clear();
            }
        }

        public int Count
        {
            get
            {
                lock (m_lock)
                {
                    return m_eb_uuid.Count;
                }
            }
        }

        public bool ContainsKey(UUID id)
        {
            lock (m_lock)
            {
                try
                {
                    return m_eb_uuid.ContainsKey(id);
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool ContainsKey(uint localID)
        {
            lock (m_lock)
            {
                try
                {
                    return m_eb_localID.ContainsKey(localID);
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool Remove(uint localID)
        {
            lock (m_lock)
            {
                try
                {
                    bool a = m_eb_uuid.Remove(m_eb_localID[localID].UUID);
                    if (!a)
                    {
                        m_log.ErrorFormat("[EntityManager]:  Remove(localID) m_eb_uuid: Remove Entity failed for {0}", localID);
                    }

                    bool b = m_eb_localID.Remove(localID);
                    if (!b)
                    {
                        m_log.ErrorFormat("[EntityManager]:  Remove(localID) m_eb_localID: Remove Entity failed for {0}", localID);
                    }

                    return a && b;
                }
                catch (Exception)
                {
                    m_log.ErrorFormat("Remove Entity failed for {0}", localID);
                    return false;
                }
            }
        }

        public bool Remove(UUID id)
        {
            lock (m_lock)
            {
                try
                {
                    bool a = m_eb_localID.Remove(m_eb_uuid[id].LocalId);
                    if (!a)
                    {
                        m_log.ErrorFormat("[EntityManager]:  Remove(UUID) m_eb_localID: Remove Entity failed for {0}", id);
                    }

                    bool b = m_eb_uuid.Remove(id);
                    if (!b)
                    {
                        m_log.ErrorFormat("[EntityManager]:  Remove(UUID) m_eb_uuid: Remove Entity failed for {0}", id);
                    }

                    return a && b;
                }
                catch (Exception)
                {
                    m_log.ErrorFormat("Remove Entity failed for {0}", id);
                    return false;
                }
            }
        }

        public List<EntityBase> GetAllByType<T>()
        {
            List<EntityBase> tmp = new List<EntityBase>();

            lock (m_lock)
            {
                try
                {
                    foreach (KeyValuePair<UUID, EntityBase> pair in m_eb_uuid)
                    {
                        if (pair.Value is T)
                        {
                            tmp.Add(pair.Value);
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("GetAllByType failed for {0}", e);
                    tmp = null;
                }
            }

            return tmp;
        }

        public List<EntityBase> GetEntities()
        {
            lock (m_lock)
            {
                return new List<EntityBase>(m_eb_uuid.Values);
            }
        }

        public EntityBase this[UUID id]
        {
            get
            {
                lock (m_lock)
                {
                    try
                    {
                        return m_eb_uuid[id];
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            set
            {
                Add(value);
            }
        }

        public EntityBase this[uint localID]
        {
            get
            {
                lock (m_lock)
                {
                    try
                    {
                        return m_eb_localID[localID];
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            set
            {
                Add(value);
            }
        }

        public bool TryGetValue(UUID key, out EntityBase obj)
        {
            lock (m_lock)
            {
                return m_eb_uuid.TryGetValue(key, out obj);
            }
        }

        public bool TryGetValue(uint key, out EntityBase obj)
        {
            lock (m_lock)
            {
                return m_eb_localID.TryGetValue(key, out obj);
            }
        }

        /// <summary>
        /// This could be optimised to work on the list 'live' rather than making a safe copy and iterating that.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<EntityBase> GetEnumerator()
        {
            return GetEntities().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


        internal void SwapChildToRootAgent(UUID userId, uint oldLocalId, uint newLocalId)
        {
            lock (m_lock)
            {
                try
                {
                    EntityBase e;
                    if (!m_eb_uuid.TryGetValue(userId, out e))
                    {
                        m_log.ErrorFormat("[EntityManager]:  SwapChildToRootAgent(UUID) could not find agent with ID {0} to swap", userId);
                        return;
                    }

                    bool a = m_eb_localID.Remove(oldLocalId);
                    if (!a)
                    {
                        m_log.ErrorFormat("[EntityManager]:  SwapChildToRootAgent(UUID) could not find agent with local ID {0} to swap", oldLocalId);
                        return;
                    }

                    m_eb_localID.Add(newLocalId, e);

                    return;
                }
                catch (Exception)
                {
                    m_log.ErrorFormat("[EntityManager]: SwapChildToRootAgent Entity failed for {0}", userId);
                    return;
                }
            }
        }
    }
}
