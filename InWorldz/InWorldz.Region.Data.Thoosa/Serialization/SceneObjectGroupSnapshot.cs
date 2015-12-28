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
using System.Text;
using OpenSim.Region.Framework.Scenes;
using ProtoBuf;
using OpenSim.Region.Framework.Scenes.Serialization;

namespace InWorldz.Region.Data.Thoosa.Serialization
{
    /// <summary>
    /// A snapshot in time of the current state of a SceneObjectGroup that is 
    /// also protobuf serializable
    /// </summary>
    [ProtoContract]
    public class SceneObjectGroupSnapshot
    {
        [ProtoMember(1)]
        public SceneObjectPartSnapshot RootPart;

        [ProtoMember(2)]
        public SceneObjectPartSnapshot[] ChildParts;

        /// <summary>
        /// Whether or not this attachment needs to be stored. Serialized so that we
        /// can send it around between regions. That way we remember to save the
        /// attachment when the user finally logs out from a region they didnt make
        /// the edits from
        /// </summary>
        [ProtoMember(3)]
        public bool TaintedAttachment;

        /// <summary>
        /// Whether or not this is a temporary attachment that should not be saved
        /// back to inventory
        /// </summary>
        [ProtoMember(4)]
        public bool TempAttachment;


        static SceneObjectGroupSnapshot()
        {
            ProtoBuf.Serializer.PrepareSerializer<SceneObjectGroupSnapshot>();
        }

        /// <summary>
        /// Creates a new snapshot from the given group
        /// </summary>
        /// <param name="sog"></param>
        /// <returns></returns>
        public static SceneObjectGroupSnapshot FromSceneObjectGroup(SceneObjectGroup sog, SerializationFlags flags)
        {
            SceneObjectGroupSnapshot snapshot = new SceneObjectGroupSnapshot
            {
                RootPart = SceneObjectPartSnapshot.FromSceneObjectPart(sog.RootPart, flags)
            };

            var parts = sog.GetParts();
            SceneObjectPartSnapshot[] partsSnap;

            //do we have more than just the root?
            if (parts.Count > 1)
            {
                List<SceneObjectPartSnapshot> partsCollect = new List<SceneObjectPartSnapshot>();

                foreach (SceneObjectPart part in parts)
                {
                    if (!part.IsRootPart())
                    {
                        partsCollect.Add(SceneObjectPartSnapshot.FromSceneObjectPart(part, flags));
                    }
                }

                partsSnap = partsCollect.ToArray();
            }
            else
            {
                //nope, just the root
                partsSnap = new SceneObjectPartSnapshot[0];
            }

            snapshot.ChildParts = partsSnap;

            if (sog.IsAttachment && sog.HasGroupChanged)
            {
                snapshot.TaintedAttachment = true;
            }
            else
            {
                snapshot.TaintedAttachment = false;
            }

            if (sog.IsAttachment && sog.IsTempAttachment)
            {
                snapshot.TempAttachment = true;
            }
            else
            {
                snapshot.TempAttachment = false;
            }


            return snapshot;
        }

        internal static SceneObjectGroupSnapshot FromBytes(byte[] bytes, int start)
        {
            using (MemoryStream ms = new MemoryStream(bytes, start, bytes.Length - start))
            {
                return ProtoBuf.Serializer.Deserialize<SceneObjectGroupSnapshot>(ms);
            }
        }

        internal SceneObjectGroup ToSceneObjectGroup()
        {
            SceneObjectGroup sog = new SceneObjectGroup();

            SceneObjectPart rootPart = RootPart.ToSceneObjectPart();

            sog.SetRootPart(rootPart);

            if (ChildParts != null)
            {
                foreach (var partSnap in ChildParts)
                {
                    SceneObjectPart childPart = partSnap.ToSceneObjectPart();

                    int originalLinkNum = childPart.LinkNum;
                    sog.AddPart(childPart);

                    // SceneObjectGroup.AddPart() tries to be smart and automatically set the LinkNum.
                    // We override that here
                    if (originalLinkNum != 0)
                        childPart.LinkNum = originalLinkNum;

                    childPart.StoreUndoState();
                }
            }

            sog.TaintedAttachment = this.TaintedAttachment;
            sog.IsTempAttachment = this.TempAttachment;

            return sog;
        }

        internal void SerializeToStream(Stream s)
        {
            ProtoBuf.Serializer.Serialize<SceneObjectGroupSnapshot>(s, this);
        }
    }
}
