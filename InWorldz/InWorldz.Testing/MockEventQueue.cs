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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenSim.Region.Framework.Interfaces;

namespace InWorldz.Testing
{
    /// <summary>
    /// Event queue for testing purposes that does nothing
    /// </summary>
    public class MockEventQueue : IEventQueue
    {
        public bool Enqueue(OpenMetaverse.StructuredData.OSD o, OpenMetaverse.UUID avatarID)
        {
            return true;
        }

        public void DisableSimulator(ulong handle, OpenMetaverse.UUID avatarID)
        {
        }

        public bool EnableSimulator(ulong handle, System.Net.IPEndPoint endPoint, OpenMetaverse.UUID avatarID)
        {
            return true;
        }

        public bool EstablishAgentCommunication(OpenMetaverse.UUID avatarID, System.Net.IPEndPoint endPoint, string capsPath)
        {
            return true;
        }

        public bool TeleportFinishEvent(ulong regionHandle, byte simAccess, System.Net.IPEndPoint regionExternalEndPoint, uint locationID, uint flags, string capsURL, OpenMetaverse.UUID agentID)
        {
            return true;
        }

        public bool CrossRegion(ulong handle, OpenMetaverse.Vector3 pos, OpenMetaverse.Vector3 lookAt, System.Net.IPEndPoint newRegionExternalEndPoint, string capsURL, OpenMetaverse.UUID avatarID, OpenMetaverse.UUID sessionID)
        {
            return true;
        }

        public void ChatterboxInvitation(OpenMetaverse.UUID sessionID, string sessionName, OpenMetaverse.UUID fromAgent, string message, OpenMetaverse.UUID toAgent, string fromName, byte dialog, uint timeStamp, bool offline, int parentEstateID, OpenMetaverse.Vector3 position, uint ttl, OpenMetaverse.UUID transactionID, bool fromGroup, byte[] binaryBucket)
        {
        }

        public void ChatterBoxSessionAgentListUpdates(OpenMetaverse.UUID sessionID, OpenMetaverse.UUID fromAgent, OpenMetaverse.UUID toAgent, bool canVoiceChat, bool isModerator, bool textMute, byte dialog)
        {
        }

        public void ParcelProperties(OpenMetaverse.Messages.Linden.ParcelPropertiesMessage parcelPropertiesMessage, OpenMetaverse.UUID avatarID)
        {
        }

        public void GroupMembership(OpenMetaverse.Packets.AgentGroupDataUpdatePacket groupUpdate, OpenMetaverse.UUID avatarID)
        {
        }

        public void ScriptRunning(OpenMetaverse.UUID objectID, OpenMetaverse.UUID itemID, bool running, bool mono, OpenMetaverse.UUID avatarID)
        {
        }

        public void QueryReply(OpenMetaverse.Packets.PlacesReplyPacket groupUpdate, OpenMetaverse.UUID avatarID)
        {
        }

        public void PartPhysicsProperties(uint localID, byte physhapetype, float density, float friction, float bounce, float gravmod, OpenMetaverse.UUID avatarID)
        {
        }

        public OpenMetaverse.StructuredData.OSD BuildEvent(string eventName, OpenMetaverse.StructuredData.OSD eventBody)
        {
            return new OpenMetaverse.StructuredData.OSD();
        }
    }
}
