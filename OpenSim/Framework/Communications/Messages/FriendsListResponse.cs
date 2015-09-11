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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ProtoBuf;
using OpenMetaverse;

namespace OpenSim.Framework.Communications.Messages
{
    /// <summary>
    /// Message that corresponds to the response from a GET on /get_user_friend_list2/. This returns a List<FriendListItem> to the caller.
    /// </summary>
    [ProtoContract]
    public class FriendsListResponse
    {
        [ProtoMember(1)]
        private List<FriendListItem> FriendList;

        static FriendsListResponse()
        {
            ProtoBuf.Serializer.PrepareSerializer<FriendsListResponse>();
        }

        public static byte[] ToBytes(List<FriendListItem> friends)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize<List<FriendListItem>>(ms, friends);
                return ms.ToArray();
            }
        }
        public Dictionary<UUID, FriendListItem> ToDict()
        {
            // This dictionary is a reverse index, indexed by the friends, not the friends list owner.
            Dictionary<UUID, FriendListItem> results = new Dictionary<UUID, FriendListItem>();
            if (FriendList != null) // Protobuf returns null for empty lists!
            {
                foreach (FriendListItem item in FriendList)
                {
                    results.Add(item.Friend, item);
                }
            }

            return results;
        }

        public static List<FriendListItem> DeserializeFromStream(Stream stream)
        {
            List<FriendListItem> friends = ProtoBuf.Serializer.Deserialize<List<FriendListItem>>(stream);
            if (friends == null)    // Protobuf returns null for empty lists!
                friends = new List<FriendListItem>();
            return friends;
        }

        public byte[] SerializeToBytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize<List<FriendListItem>>(ms, FriendList);
                return ms.ToArray();
            }
        }
    }
}
