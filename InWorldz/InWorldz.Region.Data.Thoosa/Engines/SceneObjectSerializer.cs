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
using OpenSim.Region.Framework.Scenes.Serialization;
using System.IO;

namespace InWorldz.Region.Data.Thoosa.Engines
{
    public class SceneObjectSerializer : ISceneObjectSerializer
    {
        #region ISceneObjectSerializer Members

        public byte[] SerializePartToBytes(OpenSim.Region.Framework.Scenes.SceneObjectPart part, SerializationFlags flags)
        {
            Serialization.SceneObjectPartSnapshot snap = Serialization.SceneObjectPartSnapshot.FromSceneObjectPart(part, flags);

            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize<Serialization.SceneObjectPartSnapshot>(ms, snap);
                return ms.ToArray();
            }
        }

        public byte[] SerializeGroupToBytes(OpenSim.Region.Framework.Scenes.SceneObjectGroup group, SerializationFlags flags)
        {
            Serialization.SceneObjectGroupSnapshot snap = Serialization.SceneObjectGroupSnapshot.FromSceneObjectGroup(group, flags);

            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize<Serialization.SceneObjectGroupSnapshot>(ms, snap);
                return ms.ToArray();
            }
        }

        public OpenSim.Region.Framework.Scenes.SceneObjectPart DeserializePartFromBytes(byte[] serializedPart)
        {
            using (MemoryStream ms = new MemoryStream(serializedPart))
            {
                var snap = ProtoBuf.Serializer.Deserialize<Serialization.SceneObjectPartSnapshot>(ms);
                return snap.ToSceneObjectPart();
            }
        }

        public OpenSim.Region.Framework.Scenes.SceneObjectGroup DeserializeGroupFromBytes(byte[] serializedGroup)
        {
            using (MemoryStream ms = new MemoryStream(serializedGroup))
            {
                var snap = ProtoBuf.Serializer.Deserialize<Serialization.SceneObjectGroupSnapshot>(ms);
                return snap.ToSceneObjectGroup();
            }
        }

        #endregion
    }
}
