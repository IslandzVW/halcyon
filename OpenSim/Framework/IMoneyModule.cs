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

using OpenMetaverse;

namespace OpenSim.Framework
{
    public delegate void ObjectPaid(UUID objectID, UUID agentID, int amount);
    public interface IMoneyModule
    {
        string GetCurrencySymbol();

        // Upload-related charges
        bool UploadChargeApplies(AssetType type);
        bool UploadCovered(UUID agentID);
        void ApplyUploadCharge(UUID agentID);

        // Mesh upload charges
        int MeshUploadCharge(int meshCount, int textureCount);
        bool MeshUploadCovered(UUID agentID, int meshCount, int textureCount);
        void ApplyMeshUploadCharge(UUID agentID, int meshCount, int textureCount);

        // Group-related charges
        bool GroupCreationCovered(UUID agentID);
        void ApplyGroupCreationCharge(UUID agentID);

        // Generic functions
        bool AmountCovered(UUID agentID, int amount);
        UUID ApplyCharge(UUID agentID, int transCode, int transAmount, string transDesc);
        string ObjectGiveMoney(UUID objectID, UUID fromID, UUID toID, int amount, out string reason);
        EconomyData GetEconomyData();

        event ObjectPaid OnObjectPaid;
    }

    public struct EconomyData
    {
        public int ObjectCapacity;
        public int ObjectCount;
        public int PriceEnergyUnit;
        public int PriceGroupCreate;
        public int PriceObjectClaim;
        public float PriceObjectRent;
        public float PriceObjectScaleFactor;
        public int PriceParcelClaim;
        public float PriceParcelClaimFactor;
        public int PriceParcelRent;
        public int PricePublicObjectDecay;
        public int PricePublicObjectDelete;
        public int PriceRentLight;
        public int PriceUpload;
        public int TeleportMinPrice;
    }
}
