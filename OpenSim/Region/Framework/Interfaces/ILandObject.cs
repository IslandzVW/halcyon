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
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    public delegate int overrideParcelMaxPrimCountDelegate(ILandObject obj);
    public delegate int overrideSimulatorMaxPrimCountDelegate(ILandObject obj);

    public interface ILandObject
    {
        int getParcelMaxPrimCount(ILandObject thisObject, bool includeBonusFactor);
        int getSimulatorMaxPrimCount(ILandObject thisObject);

        LandData landData { get; set; }
        UUID regionUUID { get; }
        string regionName { get; }
        bool containsPoint(int x, int y);
        ILandObject Copy();

        void sendLandUpdateToAvatarsOverParcel();
        void sendLandUpdateToAllAvatars();

        void sendLandProperties(int sequence_id, bool snap_selection, int request_result, IClientAPI remote_client);
        void updateLandProperties(LandUpdateArgs args, IClientAPI remote_client);
        bool DenyParcelAccess(UUID avatar, out ParcelPropertiesStatus reason);
        bool DenyParcelAccess(SceneObjectGroup group, bool checkSitters, out ParcelPropertiesStatus reason);
        bool isBannedFromLand(UUID avatar);
        bool isRestrictedFromLand(UUID avatar);
        void sendLandUpdateToClient(IClientAPI remote_client, int sequence_id, bool snap_selection);
        void sendLandUpdateToClient(IClientAPI remote_client, bool snap_selection);
        void sendLandUpdateToClient(IClientAPI remote_client);
        void SendSelectedLandUpdate(IClientAPI client);
        List<UUID> createAccessListArrayByFlag(AccessList flag);
        void sendAccessList(uint flags, IClientAPI remote_client);
        void updateAccessList(uint flags, List<ParcelManager.ParcelAccessEntry> entries, IClientAPI remote_client);
        void setLandBitmapFromByteArray();
        bool[,] getLandBitmap();
        void forceUpdateLandInfo();
        void updateLandInfoIfNeeded();
        void setLandBitmap(bool[,] bitmap);

        bool[,] getSquareLandBitmap(int start_x, int start_y, int end_x, int end_y);
        bool[,] modifyLandBitmapSquare(bool[,] land_bitmap, int start_x, int start_y, int end_x, int end_y, bool set_value);
        bool[,] mergeLandBitmaps(bool[,] bitmap_base, bool[,] bitmap_add);
        void sendForceObjectSelect(int local_id, int request_type, List<UUID> returnIDs, IClientAPI remote_client);
        void sendLandObjectOwners(IClientAPI remote_client);
        void returnObject(SceneObjectGroup obj);
        void returnLandObjects(uint type, UUID[] owners, UUID[] tasks, IClientAPI remote_client);
        int scriptedReturnLandObjectsByOwner(TaskInventoryItem scriptItem, UUID targetOwnerID);
        int scriptedReturnLandObjectsByIDs(SceneObjectPart callingPart, TaskInventoryItem scriptItem, List<UUID> IDs);
        void InspectParcelForAutoReturn();
        void resetLandPrimCounts();
        void addPrimToCount(SceneObjectGroup obj);
        void removePrimFromCount(SceneObjectGroup obj);
        void updateLandSold(UUID avatarID, UUID groupID, bool groupOwned);
        List<SceneObjectGroup> GetPrimsOverByOwner(UUID targetID, bool scriptedOnly);

        void deedToGroup(UUID groupID);

        void setParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel);
        void setSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel);

        /// <summary>
        /// Set the media url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        void SetMediaUrl(string url);
        
        /// <summary>
        /// Set the music url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        void SetMusicUrl(string url);

        /// <summary>
        /// Get the music url for this land parcel
        /// </summary>
        string GetMusicUrl();
    }
}
