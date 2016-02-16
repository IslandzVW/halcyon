using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// A collection that stores SceneObjectParts for a group and provides
    /// the most efficient access for different data types
    /// </summary>
    internal class GroupPartsCollection
    {
        /// <summary>
        /// Provides a better interface to the collection of parts when returned to the caller
        /// </summary>
        private class ReadOnlyPartsCollection : IReadOnlyCollection<SceneObjectPart>
        {
            private ImmutableDictionary<UUID, SceneObjectPart> m_collection;

            public ReadOnlyPartsCollection(ImmutableDictionary<UUID, SceneObjectPart> collection)
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

            public IEnumerator<SceneObjectPart> GetEnumerator()
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
        /// Stores a collection of all parts by their globally unique UUIDs
        /// </summary>
        private ImmutableDictionary<UUID, SceneObjectPart> m_partsByUuid =
            ImmutableDictionary.Create<UUID, SceneObjectPart>();

        /// <summary>
        /// Stores a collection of all parts by their locally unique UUIDs
        /// </summary>
        private ImmutableDictionary<uint, UUID> m_partsByLocalId =
            ImmutableDictionary.Create<uint, UUID>();


        public GroupPartsCollection()
        {
        }

        /// <summary>
        /// Returns the number of parts in this collection
        /// </summary>
        public int Count
        {
            get
            {
                return m_partsByUuid.Count;
            }
        }

        /// <summary>
        /// Adds the given part to this collection
        /// </summary>
        /// <param name="sop">The part to add</param>
        public int AddPart(SceneObjectPart sop)
        {
            lock (m_mutationLock)
            {
                UUID sopGlobal = sop.UUID;
                m_partsByUuid = m_partsByUuid.Add(sop.UUID, sop);

                //all local IDs will be 0 during the initial load
                if (sop.LocalId != 0)
                {
                    m_partsByLocalId = m_partsByLocalId.Add(sop.LocalId, sopGlobal);
                }
                
                return m_partsByUuid.Count;
            }
        }

        /// <summary>
        /// Removes the given part from this collection
        /// </summary>
        /// <param name="sop">The part to remove</param>
        public void RemovePart(SceneObjectPart sop)
        {
            lock (m_mutationLock)
            {
                m_partsByUuid = m_partsByUuid.Remove(sop.UUID);
                m_partsByLocalId = m_partsByLocalId.Remove(sop.LocalId);
            }
        }

        /// <summary>
        /// Clears all parts from this collection
        /// </summary>
        public void Clear()
        {
            lock (m_mutationLock)
            {
                m_partsByUuid = m_partsByUuid.Clear();
                m_partsByLocalId = m_partsByLocalId.Clear();
            }
        }

        /// <summary>
        /// Returns all parts in this collection
        /// </summary>
        public IReadOnlyCollection<SceneObjectPart> GetAllParts()
        {
            return new ReadOnlyPartsCollection(m_partsByUuid);
        }

        /// <summary>
        /// Calls the given function on each part we know about
        /// </summary>
        /// <param name="action">The function to call</param>
        public void ForEachPart(Action<SceneObjectPart> action)
        {
            foreach (var part in m_partsByUuid.Values)
            {
                action(part);
            }
        }

        /// <summary>
        /// Returns the part matching the given full ID or null
        /// </summary>
        /// <param name="fullId">The full ID of the part we're searching for</param>
        /// <returns>The matching part that was found or null</returns>
        public SceneObjectPart FindPartByFullId(UUID fullId)
        {
            SceneObjectPart outPart = null;
            m_partsByUuid.TryGetValue(fullId, out outPart);

            return outPart;
        }

        /// <summary>
        /// Returns the part matching the given local ID or null
        /// </summary>
        /// <param name="localId">The local ID of the part we're searching for</param>
        /// <returns>The matching part that was found or null</returns>
        public SceneObjectPart FindPartByLocalId(uint localId)
        {
            //this is a 2 part lookup to ensure consistency between the collections 
            UUID outFullId;
            if (! m_partsByLocalId.TryGetValue(localId, out outFullId))
            {
                return null;
            }

            return FindPartByFullId(outFullId);
        }

        /// <summary>
        /// Adds the given part to this collection only if it does not already exist
        /// </summary>
        /// <param name="sop">The part to add</param>
        public void AddPartIfNotExists(SceneObjectPart sop)
        {
            lock (m_mutationLock)
            {
                if (! m_partsByUuid.ContainsKey(sop.UUID))
                {
                    m_partsByUuid = m_partsByUuid.Add(sop.UUID, sop);
                    m_partsByLocalId = m_partsByLocalId.Add(sop.LocalId, sop.UUID);
                }
            }
        }

        /// <summary>
        /// Reindexes a part which has been assigned new local id
        /// </summary>
        /// <param name="part">The part to update</param>
        /// <param name="oldLocalId">The old localid</param>
        /// <param name="value">The new localid</param>
        public void PartLocalIdUpdated(SceneObjectPart part, uint oldLocalId, uint value)
        {
            if (value == oldLocalId) return; // nothing to do

            // different local IDs
            lock (m_mutationLock)
            {
                //add the new local ID then remove the old
                if (oldLocalId != 0)
                {
                    m_partsByLocalId = m_partsByLocalId.Remove(oldLocalId);
                }
                if (value != 0)
                {
                    m_partsByLocalId = m_partsByLocalId.Add(value, part.UUID);
                }
            }
        }
    }
}
