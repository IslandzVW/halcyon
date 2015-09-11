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

namespace OpenSim.Region.Framework.Scenes.Serialization
{
    /// <summary>
    /// Interface to provide serialization for scene objects (groups, prims)
    /// </summary>
    public interface ISceneObjectSerializer
    {
        /// <summary>
        /// Serializes a part to a protobuf byte array
        /// </summary>
        /// <param name="part">The part to serialize</param>
        /// <returns>A serialized part</returns>
        byte[] SerializePartToBytes(SceneObjectPart part, SerializationFlags flags);

        /// <summary>
        /// Serializes a group to a protobuf byte array
        /// </summary>
        /// <param name="group">The group to serialize</param>
        /// <returns>A serialized group</returns>
        byte[] SerializeGroupToBytes(SceneObjectGroup group, SerializationFlags flags);

        /// <summary>
        /// Deserializes a part from a byte array
        /// </summary>
        /// <param name="serializedPart">The byte array to restore</param>
        /// <returns>The deserialized part</returns>
        SceneObjectPart DeserializePartFromBytes(byte[] serializedPart);

        /// <summary>
        /// Deserializes a group from a byte array
        /// </summary>
        /// <param name="serializedGroup">The byte array to restore</param>
        /// <returns>The deserialized group</returns>
        SceneObjectGroup DeserializeGroupFromBytes(byte[] serializedGroup);
    }
}
