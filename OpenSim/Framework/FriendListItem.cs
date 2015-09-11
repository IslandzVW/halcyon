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

using System;
using OpenMetaverse;
using ProtoBuf;

namespace OpenSim.Framework
{
    [ProtoContract]
    public class FriendListItem
    {
        [ProtoMember(1)]
        public Guid FriendID;
        [ProtoMember(2)]
        public Guid FriendListOwnerID;

        // These are what the friend gives the listowner permission to do
        [ProtoMember(3)]
        public uint FriendListOwnerPerms;

        // These are what the list owner gives the friend permission to do
        [ProtoMember(4)]
        public uint FriendPerms;

        [ProtoMember(5)]
        public bool onlinestatus = false;

        static FriendListItem()
        {
            ProtoBuf.Serializer.PrepareSerializer<FriendListItem>();
        }

        // Compatibility getters/setters
        public UUID Friend
        {
            get { return new UUID(FriendID); }
            set { FriendID = value.Guid; }
        }
        public UUID FriendListOwner
        {
            get { return new UUID(FriendListOwnerID); }
            set { FriendListOwnerID = value.Guid; }
        }
    }
}
