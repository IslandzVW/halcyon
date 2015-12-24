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
        /// Provides a better interface to the collection of parts
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
        public void AddPart(SceneObjectPart sop)
        {
            lock (m_mutationLock)
            {
                UUID sopGlobal = sop.UUID;
                m_partsByUuid = m_partsByUuid.Add(sop.UUID, sop);
                m_partsByLocalId = m_partsByLocalId.Add(sop.LocalId, sopGlobal);
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
        
    }
}
