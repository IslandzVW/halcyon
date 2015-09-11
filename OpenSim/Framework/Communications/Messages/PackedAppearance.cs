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

namespace OpenSim.Framework.Communications.Messages
{
    /// <summary>
    /// Network-ready message to pack up an avatars appearance to send over the wire
    /// </summary>
    [ProtoContract]
    public class PackedAppearance
    {
        [ProtoMember(1)]
        public byte[] TextureEntry;

        [ProtoMember(2)]
        public byte[] VisualParams;

        [ProtoMember(3)]
        public PackedWearable[] Wearables;

        [ProtoMember(4)]
        public int Serial;


        internal static PackedAppearance FromAppearance(AvatarAppearance avatarAppearance)
        {
            PackedAppearance app = new PackedAppearance
            {
                TextureEntry = avatarAppearance.Texture.GetBytes(),
                VisualParams = avatarAppearance.VisualParams,
                Wearables = PackedWearable.FromWearables(avatarAppearance.GetWearables()),
                Serial = avatarAppearance.Serial
            };

            return app;
        }

        internal AvatarAppearance ToAppearance(OpenMetaverse.UUID owner)
        {
            AvatarAppearance app =
                new AvatarAppearance
                {
                    Owner = owner,
                    Serial = this.Serial,
                    VisualParams = this.VisualParams
                };

            List<AvatarWearable> wearables = new List<AvatarWearable>();

            for (int i = 0; i < AvatarWearable.MAX_WEARABLES && i < this.Wearables.Length; i++)
            {
                OpenMetaverse.UUID itemID = new OpenMetaverse.UUID(this.Wearables[i].ItemId);
                OpenMetaverse.UUID assetID = new OpenMetaverse.UUID(this.Wearables[i].AssetId);

                wearables.Add(new AvatarWearable(i, itemID, assetID));
            }

            app.SetWearables(wearables);

            var te = new OpenMetaverse.Primitive.TextureEntry(this.TextureEntry, 0, this.TextureEntry.Length);
            app.SetTextureEntries(te);

            app.SetHeight();

            return app;
        }
    }
}
