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

using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System;

namespace OpenSim.Data.Null
{
    /// <summary>
    /// NULL DataStore, do not store anything
    /// </summary>
    public class NullDataStore : IRegionDataStore
    {
        public void Initialize(string dbfile)
        {
            return;
        }

        public void Dispose()
        {
        }

        public void StoreRegionSettings(RegionSettings rs)
        {
        }

        public RegionSettings LoadRegionSettings(UUID regionUUID)
        {
            return null;
        }

        public void RemoveObject(UUID obj, UUID regionUUID)
        {
        }

        // see IRegionDatastore
        public void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)
        {
        }

        public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
        {
            return new List<SceneObjectGroup>();
        }

        public void StoreTerrain(double[,] ter, UUID regionID, int rev)
        {
        }

        public Tuple<double[,], int> LoadTerrain(UUID regionID)
        {
            return null;
        }

        public void RemoveLandObject(UUID globalID)
        {
        }

        public void StoreLandObject(ILandObject land)
        {
        }

        public List<LandData> LoadLandObjects(UUID regionUUID)
        {
            return new List<LandData>();
        }

        public void Shutdown()
        {
        }

        #region IRegionDataStore Members


        public void BulkStoreObjects(IEnumerable<SceneObjectGroup> group)
        {
            throw new System.NotImplementedException();
        }

        #endregion

        #region IRegionDataStore Members


        public void BulkRemoveObjects(IEnumerable<SceneObjectGroup> groups)
        {
            throw new System.NotImplementedException();
        }

        #endregion

        #region IRegionDataStore Members


        public void BulkStoreObjectInventories(IEnumerable<KeyValuePair<UUID, IEnumerable<TaskInventoryItem>>> objectInventories)
        {
            throw new System.NotImplementedException();
        }

        #endregion

        #region IRegionDataStore Members


        public void BulkStoreObjectInventories(IEnumerable<KeyValuePair<UUID, IEnumerable<TaskInventoryItem>>> objectInventories, IEnumerable<KeyValuePair<UUID, IEnumerable<UUID>>> deletedInventoryItems)
        {
            
        }

        public string LoadRegionEnvironmentString(UUID regionID)
        {
            return null;
        }
        public void StoreRegionEnvironmentString(UUID regionID, string data)
        {
        }

        public void RemoveRegionEnvironment(UUID regionID)
        {
        }

#if USE_REGION_ENVIRONMENT_DATA
        public RegionEnvironmentData LoadRegionEnvironmentData(UUID regionID)
        {
            return null;
        }
        public void StoreRegionEnvironmentData(RegionEnvironmentData env)
        {
        }
#endif
        #endregion
    }
}
