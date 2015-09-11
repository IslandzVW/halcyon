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

namespace InWorldz.Data.Assets.Stratus
{
    /// <summary>
    /// An asset in protobuf format for storage inside a stratus engine
    /// </summary>
    [ProtoContract]
    public class StratusAsset
    {
        [ProtoMember(1)]
        public Guid Id;
 
        [ProtoMember(2)]
        public sbyte Type;

        [ProtoMember(3)]
        public bool Local;

        [ProtoMember(4)]
        public bool Temporary;

        [ProtoMember(5)]
        public DateTime CreateTime;

        [ProtoMember(6)]
        public string Name;

        [ProtoMember(7)]
        public string Description;

        [ProtoMember(8)]
        public byte[] Data;

        [ProtoMember(9)]
        public uint StorageFlags;

        /// <summary>
        /// Converts from a stratus asset to an opensim asset
        /// </summary>
        /// <returns></returns>
        internal OpenSim.Framework.AssetBase ToAssetBase()
        {
            OpenSim.Framework.AssetBase asset = new OpenSim.Framework.AssetBase
            {
                Data = this.Data,
                Description = this.Description,
                ID = new OpenMetaverse.UUID(this.Id).ToString(),
                FullID = new OpenMetaverse.UUID(this.Id),
                Local = this.Local,
                Name = this.Name,
                Temporary = this.Temporary,
                Type = this.Type,
            };

            asset.Metadata.CreationDate = CreateTime;

            return asset;
        }

        /// <summary>
        /// Converts from an opensim asset to a stratus asset
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        internal static StratusAsset FromAssetBase(OpenSim.Framework.AssetBase asset)
        {
            return new StratusAsset
            {
                CreateTime = asset.Metadata.CreationDate,
                Data = asset.Data,
                Description = asset.Description,
                Id = asset.FullID.Guid,
                Local = asset.Local,
                Name = asset.Name,
                Temporary = asset.Temporary,
                Type = asset.Type
            };
        }

        public static string GetProto()
        {
            return ProtoBuf.Serializer.GetProto<StratusAsset>();
        }
    }
}
