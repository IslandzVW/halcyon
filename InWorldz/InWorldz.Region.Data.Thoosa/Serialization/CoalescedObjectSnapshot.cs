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
using ProtoBuf;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Framework;
using System.IO;

namespace InWorldz.Region.Data.Thoosa.Serialization
{
    /// <summary>
    /// A protobuf serializable snapshot in time of a coalesced object
    /// </summary>
    [ProtoContract]
    public class CoalescedObjectSnapshot
    {
        [ProtoMember(1)]
        public Tuple<SceneObjectGroupSnapshot, ItemPermissionBlockSnapshot>[] GroupsWithPermissions;

        public static CoalescedObjectSnapshot FromGroups(IEnumerable<Tuple<SceneObjectGroup,ItemPermissionBlock>> groups, SerializationFlags flags)
        {
            List<Tuple<SceneObjectGroupSnapshot, ItemPermissionBlockSnapshot>> snapGroups
                = new List<Tuple<SceneObjectGroupSnapshot, ItemPermissionBlockSnapshot>>();

            foreach (var group in groups)
            {
                snapGroups.Add(
                    new Tuple<SceneObjectGroupSnapshot, ItemPermissionBlockSnapshot>(
                        SceneObjectGroupSnapshot.FromSceneObjectGroup(group.Item1, flags),
                        ItemPermissionBlockSnapshot.FromItemPermissionBlock(group.Item2))
                    );
            }

            return new CoalescedObjectSnapshot { GroupsWithPermissions = snapGroups.ToArray() };
        }

        public static CoalescedObjectSnapshot FromCoalescedObject(CoalescedObject csog, SerializationFlags flags)
        {
            List<Tuple<SceneObjectGroupSnapshot, ItemPermissionBlockSnapshot>> snapGroups
                = new List<Tuple<SceneObjectGroupSnapshot, ItemPermissionBlockSnapshot>>();

            foreach (var group in csog.Groups)
            {
                snapGroups.Add(
                    new Tuple<SceneObjectGroupSnapshot, ItemPermissionBlockSnapshot>(
                        SceneObjectGroupSnapshot.FromSceneObjectGroup(group, flags),
                        ItemPermissionBlockSnapshot.FromItemPermissionBlock(csog.FindPermissions(group.UUID)))
                    );
            }

            return new CoalescedObjectSnapshot { GroupsWithPermissions = snapGroups.ToArray() };
        }

        public void SerializeToStream(Stream s)
        {
            ProtoBuf.Serializer.Serialize<CoalescedObjectSnapshot>(s, this);
        }

        public static CoalescedObjectSnapshot FromBytes(byte[] bytes, int index)
        {
            using (MemoryStream ms = new MemoryStream(bytes, index, bytes.Length - index))
            {
                return ProtoBuf.Serializer.Deserialize<CoalescedObjectSnapshot>(ms);
            }
        }

        public CoalescedObject ToCoalescedObject()
        {
            List<SceneObjectGroup> groups = new List<SceneObjectGroup>();
            Dictionary<OpenMetaverse.UUID, ItemPermissionBlock> perms = new Dictionary<OpenMetaverse.UUID, ItemPermissionBlock>();

            foreach (var groupPermTuple in this.GroupsWithPermissions)
            {
                SceneObjectGroup sog = groupPermTuple.Item1.ToSceneObjectGroup();
                groups.Add(sog);
                perms.Add(sog.UUID, groupPermTuple.Item2.ToItemPermissionBlock(sog.UUID));
            }

            return new CoalescedObject(groups, perms);
        }
    }
}
