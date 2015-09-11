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

using System.Net;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IEventQueue
    {
        bool Enqueue(OSD o, UUID avatarID);

        // These are required to decouple Scenes from EventQueueHelper
        void DisableSimulator(ulong handle, UUID avatarID);
        bool EnableSimulator(ulong handle, IPEndPoint endPoint, UUID avatarID);
        bool EstablishAgentCommunication(UUID avatarID, IPEndPoint endPoint, 
                                         string capsPath);
        bool TeleportFinishEvent(ulong regionHandle, byte simAccess, 
                                 IPEndPoint regionExternalEndPoint,
                                 uint locationID, uint flags, string capsURL, 
                                 UUID agentID);
        bool CrossRegion(ulong handle, Vector3 pos, Vector3 lookAt,
                         IPEndPoint newRegionExternalEndPoint,
                         string capsURL, UUID avatarID, UUID sessionID);
        void ChatterboxInvitation(UUID sessionID, string sessionName,
                                  UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog,
                                  uint timeStamp, bool offline, int parentEstateID, Vector3 position,
                                  uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket);
        void ChatterBoxSessionAgentListUpdates(UUID sessionID, UUID fromAgent, UUID toAgent, bool canVoiceChat, 
                                               bool isModerator, bool textMute, byte dialog);
        void ParcelProperties(ParcelPropertiesMessage parcelPropertiesMessage, UUID avatarID);
        void GroupMembership(AgentGroupDataUpdatePacket groupUpdate, UUID avatarID);
        void ScriptRunning(UUID objectID, UUID itemID, bool running, bool mono, UUID avatarID);
        void QueryReply(PlacesReplyPacket groupUpdate, UUID avatarID);
        void PartPhysicsProperties(uint localID, byte physhapetype, float density, float friction, float bounce, float gravmod, UUID avatarID);

        OSD BuildEvent(string eventName, OSD eventBody);
    }
}
