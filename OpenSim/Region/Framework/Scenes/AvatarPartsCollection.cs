using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// A collection that stores ScenePresences for a group or part
    /// and provides the most efficient access for different data types.
    /// Corresponds to the ScenePresence version of GroupPartsCollection
    /// but may be a per-group (SOG) or per-part (SOP) list.
    /// </summary>
    internal class AvatarPartsCollection
    {
        /// <summary>
        /// Provides a better interface to the collection of parts when returned to the caller
        /// </summary>
        private class ReadOnlyAvatarsCollection : IReadOnlyCollection<ScenePresence>
        {
            private ImmutableDictionary<UUID, ScenePresence> m_collection;

            public ReadOnlyAvatarsCollection(ImmutableDictionary<UUID, ScenePresence> collection)
            {
                m_collection = collection;
            }

            public int Count
            {
                get
                {
                    return m_collection.Count;
                }
            }

            public IEnumerator<ScenePresence> GetEnumerator()
            {
                return m_collection.Values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return m_collection.Values.GetEnumerator();
            }
        }

        /// <summary>
        /// Lock held for mutations only
        /// </summary>
        private object m_mutationLock = new object();

        /// <summary>
        /// Stores a collection of all avatars by their globally unique UUIDs
        /// </summary>
        private ImmutableDictionary<UUID, ScenePresence> m_avatarsByUuid =
            ImmutableDictionary.Create<UUID, ScenePresence>();

        /// <summary>
        /// Stores a collection of all parts by their locally unique UUIDs
        /// </summary>
        private ImmutableDictionary<uint, UUID> m_avatarsByLocalId =
            ImmutableDictionary.Create<uint, UUID>();

        // Represents a collection of avatar-as-a-prim, either in 
        // a group list (all avatars) or those that sat on a specific prim.
        public AvatarPartsCollection()
        {
        }

        /// <summary>
        /// Returns the number of avatars in this collection
        /// </summary>
        public int Count
        {
            get
            {
                return m_avatarsByUuid.Count;
            }
        }

        /// <summary>
        /// Adds the given avatar to this collection
        /// </summary>
        /// <param name="sp">The avatar to add</param>
        public bool AddAvatar(ScenePresence sp)
        {
            lock (m_mutationLock)
            {
                if (!m_avatarsByUuid.ContainsKey(sp.UUID))
                {
                    m_avatarsByUuid = m_avatarsByUuid.Add(sp.UUID, sp);

                    //all local IDs will be 0 during the initial load
                    if (sp.LocalId != 0)
                        m_avatarsByLocalId = m_avatarsByLocalId.Add(sp.LocalId, sp.UUID);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Removes the given avatar from this collection
        /// </summary>
        /// <param name="sp">The avatar to remove</param>
        public bool RemoveAvatar(ScenePresence sp)
        {
            lock (m_mutationLock)
            {
                if (m_avatarsByUuid.ContainsKey(sp.UUID))
                {
                    m_avatarsByUuid = m_avatarsByUuid.Remove(sp.UUID);
                    m_avatarsByLocalId = m_avatarsByLocalId.Remove(sp.LocalId);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Removes the given avatar from this collection
        /// </summary>
        /// <param name="sp">The avatar to remove</param>
        public void RemovePart(UUID avatarID)
        {
            lock (m_mutationLock)
            {
                ScenePresence sp;
                if (m_avatarsByUuid.TryGetValue(avatarID, out sp))
                {
                    RemoveAvatar(sp);
                }
            }
        }

        /// <summary>
        /// Clears all parts from this collection
        /// </summary>
        public void Clear()
        {
            lock (m_mutationLock)
            {
                m_avatarsByUuid = m_avatarsByUuid.Clear();
                m_avatarsByLocalId = m_avatarsByLocalId.Clear();
            }
        }

        /// <summary>
        /// Returns all parts in this collection
        /// </summary>
        public IReadOnlyCollection<ScenePresence> GetAllParts()
        {
            return new ReadOnlyAvatarsCollection(m_avatarsByUuid);
        }

        /// <summary>
        /// Calls the given function on each avatar we know about
        /// </summary>
        /// <param name="action">The function to call</param>
        public void ForEach(Action<ScenePresence> action)
        {
            foreach (var sp in m_avatarsByUuid.Values)
            {
                action(sp);
            }
        }

        /// <summary>
        /// Returns the avatar matching the given full ID or null
        /// </summary>
        /// <param name="fullId">The full ID of the avatar we're searching for</param>
        /// <returns>The matching avatar that was found or null</returns>
        public ScenePresence FindPartByFullId(UUID fullId)
        {
            ScenePresence outPart = null;
            m_avatarsByUuid.TryGetValue(fullId, out outPart);

            return outPart;
        }

        /// <summary>
        /// Returns the avatar matching the given local ID or null
        /// </summary>
        /// <param name="localId">The local ID of the avatar we're searching for</param>
        /// <returns>The matching avatar that was found or null</returns>
        public ScenePresence FindPartByLocalId(uint localId)
        {
            //this is a 2 avatar lookup to ensure consistency between the collections 
            UUID outFullId;
            if (! m_avatarsByLocalId.TryGetValue(localId, out outFullId))
            {
                return null;
            }

            return FindPartByFullId(outFullId);
        }

        /// <summary>
        /// Adds the given avatar to this collection only if it does not already exist
        /// </summary>
        /// <param name="sp">The avatar to add</param>
        public void AddPartIfNotExists(ScenePresence sp)
        {
            lock (m_mutationLock)
            {
                if (! m_avatarsByUuid.ContainsKey(sp.UUID))
                {
                    m_avatarsByUuid = m_avatarsByUuid.Add(sp.UUID, sp);
                    m_avatarsByLocalId = m_avatarsByLocalId.Add(sp.LocalId, sp.UUID);
                }
            }
        }

        /// <summary>
        /// Reindexes a avatar which has been assigned new local id
        /// </summary>
        /// <param name="sp">The part to update</param>
        /// <param name="oldLocalId">The old localid</param>
        /// <param name="value">The new localid</param>
        public void AvatarLocalIdUpdated(ScenePresence sp, uint oldLocalId, uint value)
        {
            if (value == oldLocalId) return; // nothing to do

            // different local IDs
            lock (m_mutationLock)
            {
                //add the new local ID then remove the old
                if (oldLocalId != 0)
                {
                    m_avatarsByLocalId = m_avatarsByLocalId.Remove(oldLocalId);
                }
                if (value != 0)
                {
                    m_avatarsByLocalId = m_avatarsByLocalId.Add(value, sp.UUID);
                }
            }
        }
    }
}
